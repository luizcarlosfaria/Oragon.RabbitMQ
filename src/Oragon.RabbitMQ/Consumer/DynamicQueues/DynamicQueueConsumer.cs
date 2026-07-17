// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Default implementation of <see cref="IAmqpDynamicQueueConsumer"/>.
/// </summary>
public sealed class DynamicQueueConsumer : IAmqpDynamicQueueConsumer
{
    private static readonly IReadOnlyDictionary<string, object> s_emptyMetadata =
        new Dictionary<string, object>();

    private readonly IServiceProvider serviceProvider;
    private readonly IAmqpContextAccessor contextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicQueueConsumer"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    /// <param name="contextAccessor">AMQP context accessor.</param>
    public DynamicQueueConsumer(IServiceProvider serviceProvider, IAmqpContextAccessor contextAccessor)
    {
        this.serviceProvider = serviceProvider;
        this.contextAccessor = contextAccessor;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Dynamic consumer reports unexpected failures as a faulted consumption result.")]
    public async Task<DynamicQueueConsumeResult> ConsumeAsync<T>(
        DynamicQueueConsumeRequest<T> request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request, cancellationToken);

        IReadOnlyDictionary<string, object> metadata = request.Metadata ?? s_emptyMetadata;
        var stopwatch = Stopwatch.StartNew();

        IConnection connection = await this.ResolveConnectionAsync(request, cancellationToken).ConfigureAwait(true);
        IChannel channel = await this.CreateChannelAsync(request, connection, cancellationToken).ConfigureAwait(true);

        DynamicQueueConsumeResult result;
        bool closeChannelOnExit = true;

        try
        {
            QueueDeclareOk queueDeclare = await channel.QueueDeclarePassiveAsync(request.QueueName, cancellationToken).ConfigureAwait(true);
            long initialReadyCount = queueDeclare.MessageCount;

            DynamicQueueStartDecision startDecision = request.BeforeStartAsync == null
                ? DynamicQueueStartDecision.Allow()
                : await request.BeforeStartAsync(
                    new DynamicQueueStartContext(request.QueueName, initialReadyCount, this.serviceProvider, metadata),
                    cancellationToken).ConfigureAwait(true);

            if (startDecision.Type != DynamicQueueStartDecisionType.Allow)
            {
                result = BuildStartDecisionResult(request.QueueName, initialReadyCount, stopwatch.Elapsed, startDecision);
                await this.InvokeAfterStopAsync(request, metadata, result, cancellationToken).ConfigureAwait(true);
                return result;
            }

            if (initialReadyCount == 0 && request.StopAfterInitialQueueLength)
            {
                result = new DynamicQueueConsumeResult
                {
                    Status = DynamicQueueConsumeStatus.Empty,
                    QueueName = request.QueueName,
                    InitialReadyCount = initialReadyCount,
                    RemainingReadyCount = 0,
                    Elapsed = stopwatch.Elapsed,
                };
                await this.InvokeAfterStopAsync(request, metadata, result, cancellationToken).ConfigureAwait(true);
                return result;
            }

            result = await this.ConsumeInternalAsync(request, channel, connection, initialReadyCount, stopwatch, cancellationToken).ConfigureAwait(true);
            closeChannelOnExit = !result.InFlightDrainTimedOut;
            await this.InvokeAfterStopAsync(request, metadata, result, cancellationToken).ConfigureAwait(true);
            return result;
        }
        catch (OperationInterruptedException exception)
        {
            result = new DynamicQueueConsumeResult
            {
                Status = DynamicQueueConsumeStatus.QueueMissing,
                QueueName = request.QueueName,
                Elapsed = stopwatch.Elapsed,
                Exception = exception,
            };
            await this.InvokeAfterStopAsync(request, metadata, result, cancellationToken).ConfigureAwait(true);
            return result;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            result = new DynamicQueueConsumeResult
            {
                Status = DynamicQueueConsumeStatus.Interrupted,
                QueueName = request.QueueName,
                Elapsed = stopwatch.Elapsed,
                Exception = exception,
            };
            await this.InvokeAfterStopAsync(request, metadata, result, CancellationToken.None).ConfigureAwait(true);
            return result;
        }
        catch (Exception exception)
        {
            result = new DynamicQueueConsumeResult
            {
                Status = DynamicQueueConsumeStatus.Faulted,
                QueueName = request.QueueName,
                Elapsed = stopwatch.Elapsed,
                Exception = exception,
            };
            await this.InvokeAfterStopAsync(request, metadata, result, CancellationToken.None).ConfigureAwait(true);
            return result;
        }
        finally
        {
            if (closeChannelOnExit)
            {
                await CloseChannelAsync(channel).ConfigureAwait(true);
            }
        }
    }

    private static void ValidateRequest<T>(DynamicQueueConsumeRequest<T> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.QueueName);
        ArgumentNullException.ThrowIfNull(request.OnMessageAsync);

        if (request.PrefetchCount == 0) throw new ArgumentOutOfRangeException(nameof(request), "PrefetchCount must be greater than zero.");
        if (request.MaxLocalConcurrency == 0) throw new ArgumentOutOfRangeException(nameof(request), "MaxLocalConcurrency must be greater than zero.");
        if (request.MaxMessages.HasValue && request.MaxMessages <= 0) throw new ArgumentOutOfRangeException(nameof(request), "MaxMessages must be greater than zero.");
        if (request.MaxDuration.HasValue && request.MaxDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request), "MaxDuration must be greater than zero.");
        if (request.IdleTimeout.HasValue && request.IdleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request), "IdleTimeout must be greater than zero.");
        if (request.InFlightDrainTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request), "InFlightDrainTimeout must be greater than zero.");

        bool hasStopRule = request.MaxMessages.HasValue
            || request.MaxDuration.HasValue
            || request.IdleTimeout.HasValue
            || request.StopAfterInitialQueueLength
            || cancellationToken.CanBeCanceled;

        if (!hasStopRule)
        {
            throw new InvalidOperationException("Dynamic queue consumption requires at least one effective stop rule.");
        }
    }

    private async ValueTask<IConnection> ResolveConnectionAsync<T>(DynamicQueueConsumeRequest<T> request, CancellationToken cancellationToken)
    {
        if (request.Connection != null)
        {
            return request.Connection;
        }

        if (request.ConnectionFactory != null)
        {
            return await request.ConnectionFactory(this.serviceProvider, cancellationToken).ConfigureAwait(true);
        }

        if (this.contextAccessor.Current?.Connection != null)
        {
            return this.contextAccessor.Current.Connection;
        }

        return this.serviceProvider.GetRequiredService<IConnection>();
    }

    private async ValueTask<IChannel> CreateChannelAsync<T>(
        DynamicQueueConsumeRequest<T> request,
        IConnection connection,
        CancellationToken cancellationToken)
    {
        if (request.ChannelFactory != null)
        {
            return await request.ChannelFactory(this.serviceProvider, connection, cancellationToken).ConfigureAwait(true);
        }

        return await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Dynamic consumer converts handler failures into a faulted result.")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "When in-flight drain times out, async state remains alive so in-flight callbacks are not disrupted.")]
    [SuppressMessage("Reliability", "CA2025:Ensure tasks using IDisposable instances complete before the instances are disposed", Justification = "A timed-out drain returns to the caller and continues cleanup after in-flight callbacks finish.")]
    private async Task<DynamicQueueConsumeResult> ConsumeInternalAsync<T>(
        DynamicQueueConsumeRequest<T> request,
        IChannel channel,
        IConnection connection,
        long initialReadyCount,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        await channel.BasicQosAsync(0, request.PrefetchCount, false, cancellationToken).ConfigureAwait(true);

        var localConcurrency = new SemaphoreSlim(request.MaxLocalConcurrency, request.MaxLocalConcurrency);
        CancellationTokenSource stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var completion = new TaskCompletionSource<DynamicQueueConsumeStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(channel);
        string consumerTag = null;

        int received = 0;
        int acked = 0;
        int nacked = 0;
        int rejected = 0;
        int inFlight = 0;
        int consumerCancelRequested = 0;
        bool brokerCanceled = false;
        bool inFlightDrainTimedOut = false;
        bool disposeAsyncState = true;
        Exception failureException = null;
        DateTimeOffset lastDeliveryAt = default;

        async Task CancelConsumerAsync()
        {
            if (Interlocked.Exchange(ref consumerCancelRequested, 1) == 0
                && !string.IsNullOrWhiteSpace(consumerTag))
            {
                await channel.BasicCancelAsync(consumerTag, false, CancellationToken.None).ConfigureAwait(true);
            }
        }

        void TryComplete(DynamicQueueConsumeStatus status)
        {
            _ = completion.TrySetResult(status);
            _ = stopCts.CancelAsync();
        }

        bool TryReserveMessageSlot(out int currentReceived, out DynamicQueueConsumeStatus? stopStatus)
        {
            while (true)
            {
                int observed = Volatile.Read(ref received);

                if (request.MaxMessages.HasValue && observed >= request.MaxMessages.Value)
                {
                    currentReceived = observed;
                    stopStatus = DynamicQueueConsumeStatus.MaxMessagesReached;
                    return false;
                }

                if (request.StopAfterInitialQueueLength && observed >= initialReadyCount)
                {
                    currentReceived = observed;
                    stopStatus = DynamicQueueConsumeStatus.InitialQueueLengthReached;
                    return false;
                }

                int next = observed + 1;
                if (Interlocked.CompareExchange(ref received, next, observed) == observed)
                {
                    currentReceived = next;
                    stopStatus = GetMessageLimitStopStatus(request, initialReadyCount, next);
                    return true;
                }
            }
        }

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            _ = Interlocked.Increment(ref inFlight);
            bool localConcurrencyAcquired = false;
            bool settlementAttempted = false;

            try
            {
                await localConcurrency.WaitAsync(cancellationToken).ConfigureAwait(true);
                localConcurrencyAcquired = true;

                if (completion.Task.IsCompleted)
                {
                    await channel.BasicNackAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: CancellationToken.None).ConfigureAwait(true);
                    return;
                }

                lastDeliveryAt = DateTimeOffset.UtcNow;
                if (!TryReserveMessageSlot(out int currentReceived, out DynamicQueueConsumeStatus? stopStatus))
                {
                    await CancelConsumerAsync().ConfigureAwait(true);
                    await channel.BasicNackAsync(
                        eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: CancellationToken.None).ConfigureAwait(true);
                    TryComplete(stopStatus ?? DynamicQueueConsumeStatus.Completed);
                    return;
                }

                using IServiceScope scope = this.serviceProvider.CreateScope();

                ILogger<DynamicQueueConsumer> logger = scope.ServiceProvider.GetRequiredService<ILogger<DynamicQueueConsumer>>();

                IAmqpSerializer serializer = scope.ServiceProvider.GetRequiredService<IAmqpSerializer>();

                T message = serializer.Deserialize<T>(eventArgs);

                IAmqpContext BuildContext(CancellationToken contextToken) => new AmqpContext(logger, contextToken)
                {
                    Request = eventArgs,
                    Channel = channel,
                    Connection = connection,
                    MessageObject = message,
                    QueueName = request.QueueName,
                    Serializer = serializer,
                    ServiceProvider = scope.ServiceProvider,
                };

                // The handler observes stop rules through stopCts; settlement must survive an
                // internal stop, so it only observes the caller's token.
                IAmqpContext handlerContext = BuildContext(stopCts.Token);

                IAmqpResult messageResult = await request.OnMessageAsync(message, handlerContext).ConfigureAwait(true);
                if (stopStatus.HasValue)
                {
                    await CancelConsumerAsync().ConfigureAwait(true);
                }

                IAmqpContext settlementContext = BuildContext(cancellationToken);
                settlementAttempted = true;
                await messageResult.ExecuteAsync(settlementContext).ConfigureAwait(true);

                if (messageResult is AckResult)
                {
                    _ = Interlocked.Increment(ref acked);
                }
                else if (messageResult is NackResult)
                {
                    _ = Interlocked.Increment(ref nacked);
                }
                else if (messageResult is RejectResult)
                {
                    _ = Interlocked.Increment(ref rejected);
                }

                if (stopStatus.HasValue)
                {
                    TryComplete(stopStatus.Value);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryComplete(DynamicQueueConsumeStatus.Interrupted);
            }
            catch (OperationCanceledException) when (stopCts.IsCancellationRequested)
            {
                // An internal stop rule canceled the message context token.
                // The final cycle status is already represented by the completed stop rule.
            }
            catch (Exception exception)
            {
                failureException = exception;
                if (!settlementAttempted)
                {
                    if (await TryNackUnhandledDeliveryAsync(channel, eventArgs.DeliveryTag).ConfigureAwait(true))
                    {
                        _ = Interlocked.Increment(ref nacked);
                    }
                }

                TryComplete(DynamicQueueConsumeStatus.Faulted);
            }
            finally
            {
                if (localConcurrencyAcquired)
                {
                    _ = localConcurrency.Release();
                }

                _ = Interlocked.Decrement(ref inFlight);
            }
        };

        consumer.ShutdownAsync += (_, _) =>
        {
            brokerCanceled = true;
            TryComplete(DynamicQueueConsumeStatus.Interrupted);
            return Task.CompletedTask;
        };

        consumerTag = await channel.BasicConsumeAsync(
            queue: request.QueueName,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: true,
            exclusive: false,
            arguments: null,
            consumer: consumer,
            cancellationToken: cancellationToken).ConfigureAwait(true);

        lastDeliveryAt = DateTimeOffset.UtcNow;
        Task monitorTask = MonitorStopRulesAsync(request, completion, stopwatch, () => lastDeliveryAt, () => Volatile.Read(ref inFlight), stopCts.Token);

        DynamicQueueConsumeStatus status;
        try
        {
            status = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = DynamicQueueConsumeStatus.Interrupted;
        }

        await stopCts.CancelAsync().ConfigureAwait(true);
        await CancelConsumerAsync().ConfigureAwait(true);
        bool inFlightDrained = await WaitInFlightAsync(
            () => Volatile.Read(ref inFlight),
            request.InFlightDrainTimeout,
            CancellationToken.None).ConfigureAwait(true);
        inFlightDrainTimedOut = !inFlightDrained;
        disposeAsyncState = inFlightDrained;

        long? remainingReadyCount = await TryGetRemainingReadyCountAsync(channel, request.QueueName).ConfigureAwait(true);

        await SuppressMonitorAsync(monitorTask).ConfigureAwait(true);

        if (disposeAsyncState)
        {
            localConcurrency.Dispose();
            stopCts.Dispose();
        }
        else
        {
            _ = CleanupAfterTimedOutDrainAsync(
                () => Volatile.Read(ref inFlight),
                localConcurrency,
                stopCts,
                channel);
        }

        return new DynamicQueueConsumeResult
        {
            Status = status,
            QueueName = request.QueueName,
            InitialReadyCount = initialReadyCount,
            RemainingReadyCount = remainingReadyCount,
            MessagesReceived = received,
            MessagesAcked = acked,
            MessagesNacked = nacked,
            MessagesRejected = rejected,
            Elapsed = stopwatch.Elapsed,
            BrokerCanceledConsumer = brokerCanceled,
            InFlightDrainTimedOut = inFlightDrainTimedOut,
            Exception = failureException,
        };
    }

    private static async Task MonitorStopRulesAsync<T>(
        DynamicQueueConsumeRequest<T> request,
        TaskCompletionSource<DynamicQueueConsumeStatus> completion,
        Stopwatch stopwatch,
        Func<DateTimeOffset> lastDeliveryAt,
        Func<int> inFlight,
        CancellationToken cancellationToken)
    {
        while (!completion.Task.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            if (request.MaxDuration.HasValue && stopwatch.Elapsed >= request.MaxDuration.Value)
            {
                _ = completion.TrySetResult(DynamicQueueConsumeStatus.MaxDurationReached);
                return;
            }

            if (request.IdleTimeout.HasValue
                && inFlight() == 0
                && DateTimeOffset.UtcNow - lastDeliveryAt() >= request.IdleTimeout.Value)
            {
                _ = completion.TrySetResult(DynamicQueueConsumeStatus.IdleTimeoutReached);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(true);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best-effort cleanup for in-flight callbacks after the caller already received a timed-out drain result.")]
    private static async Task CleanupAfterTimedOutDrainAsync(
        Func<int> inFlight,
        SemaphoreSlim localConcurrency,
        CancellationTokenSource stopCts,
        IChannel channel)
    {
        try
        {
            while (inFlight() > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(true);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            localConcurrency.Dispose();
            stopCts.Dispose();
            await TryCloseChannelAsync(channel).ConfigureAwait(true);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unhandled dynamic deliveries are settled best-effort before returning a faulted result.")]
    private static async ValueTask<bool> TryNackUnhandledDeliveryAsync(IChannel channel, ulong deliveryTag)
    {
        try
        {
            await channel.BasicNackAsync(
                deliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: CancellationToken.None).ConfigureAwait(true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static DynamicQueueConsumeStatus? GetMessageLimitStopStatus<T>(
        DynamicQueueConsumeRequest<T> request,
        long initialReadyCount,
        int currentReceived)
    {
        if (request.MaxMessages.HasValue && currentReceived >= request.MaxMessages.Value)
        {
            return DynamicQueueConsumeStatus.MaxMessagesReached;
        }

        if (request.StopAfterInitialQueueLength && currentReceived >= initialReadyCount)
        {
            return DynamicQueueConsumeStatus.InitialQueueLengthReached;
        }

        return null;
    }

    private static DynamicQueueConsumeResult BuildStartDecisionResult(
        string queueName,
        long initialReadyCount,
        TimeSpan elapsed,
        DynamicQueueStartDecision startDecision)
    {
        DynamicQueueConsumeStatus status = startDecision.Type switch
        {
            DynamicQueueStartDecisionType.Skip => DynamicQueueConsumeStatus.Skipped,
            DynamicQueueStartDecisionType.Defer => DynamicQueueConsumeStatus.Deferred,
            DynamicQueueStartDecisionType.Fail => DynamicQueueConsumeStatus.Faulted,
            _ => DynamicQueueConsumeStatus.Completed,
        };

        return new DynamicQueueConsumeResult
        {
            Status = status,
            QueueName = queueName,
            InitialReadyCount = initialReadyCount,
            Elapsed = elapsed,
            Exception = startDecision.Exception,
        };
    }

    private async ValueTask InvokeAfterStopAsync<T>(
        DynamicQueueConsumeRequest<T> request,
        IReadOnlyDictionary<string, object> metadata,
        DynamicQueueConsumeResult result,
        CancellationToken cancellationToken)
    {
        if (request.AfterStopAsync != null)
        {
            await request.AfterStopAsync(
                new DynamicQueueStopContext(result, this.serviceProvider, metadata),
                cancellationToken).ConfigureAwait(true);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Remaining count is best-effort diagnostic information.")]
    private static async Task<long?> TryGetRemainingReadyCountAsync(IChannel channel, string queueName)
    {
        try
        {
            QueueDeclareOk remaining = await channel.QueueDeclarePassiveAsync(queueName, CancellationToken.None).ConfigureAwait(true);
            return remaining.MessageCount;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<bool> WaitInFlightAsync(Func<int> inFlight, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (inFlight() > 0 && !timeoutCts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), timeoutCts.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return inFlight() == 0;
    }

    private static async Task SuppressMonitorAsync(Task monitorTask)
    {
        try
        {
            await monitorTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async ValueTask CloseChannelAsync(IChannel channel)
    {
        try
        {
            if (channel.IsOpen)
            {
                await channel.CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(true);
            }
        }
        finally
        {
            channel.Dispose();
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best-effort asynchronous cleanup must not surface unobserved exceptions.")]
    private static async ValueTask TryCloseChannelAsync(IChannel channel)
    {
        try
        {
            await CloseChannelAsync(channel).ConfigureAwait(true);
        }
        catch (Exception)
        {
        }
    }
}
