// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Oragon.RabbitMQ.Consumer;


/// <summary>
/// This class is responsible for consuming messages from RabbitMQ.
/// </summary>
public class ConsumerServer : IHostedService, IAsyncDisposable
{
    private static readonly Action<ILogger, int, Exception> s_logAllConsumersStarted = LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "AllConsumersStarted"), "All {Count} consumer(s) started successfully");

    private static readonly Action<ILogger, Exception> s_logConsumerFailedToStart = LoggerMessage.Define(LogLevel.Critical, new EventId(2, "ConsumerFailedToStart"), "Consumer failed to start - configuration error detected. Failing fast.");

    private readonly ILogger<ConsumerServer> logger;
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerServer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ConsumerServer(ILogger<ConsumerServer> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Lock the consumers to Add Consumers
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// The consumers that will be started.
    /// </summary>
    public IEnumerable<IHostedAmqpConsumer> Consumers => [.. this.internalConsumers];
    private readonly List<IHostedAmqpConsumer> internalConsumers = [];

    /// <summary>
    /// The consumers that will be started.
    /// </summary>
    public IEnumerable<IConsumerDescriptor> ConsumerDescriptors => [.. this.internalConsumerDescriptors];
    private readonly List<IConsumerDescriptor> internalConsumerDescriptors = [];


    /// <summary>
    /// Add a new consumer to the server.
    /// </summary>
    /// <param name="consumerDescriptor"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void AddConsumerDescriptor(ConsumerDescriptor consumerDescriptor)
    {
        if (this.IsReadOnly) throw new InvalidOperationException("The ConsumerServer is in readonly state");

        this.internalConsumerDescriptors.Add(consumerDescriptor);
    }


    /// <summary>
    /// This method is called when the Microsoft.Extensions.Hosting.IHostedService starts.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.IsReadOnly = true;

        foreach (IConsumerDescriptor consumer in this.ConsumerDescriptors)
        {
            this.internalConsumers.Add(await consumer.BuildConsumerAsync(cancellationToken).ConfigureAwait(true));
        }

        // Start all consumers in parallel
        var startTasks = this.internalConsumers
            .Select(consumer => this.StartConsumerAsync(consumer, cancellationToken))
            .ToList();

        // Wait for ALL to complete - if any fails, propagate exception (fail-fast)
        await Task.WhenAll(startTasks).ConfigureAwait(true);

        s_logAllConsumersStarted(this.logger, this.internalConsumers.Count, null);
    }

    private async Task StartConsumerAsync(IHostedAmqpConsumer consumer, CancellationToken cancellationToken)
    {
        try
        {
            await consumer.StartAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            s_logConsumerFailedToStart(this.logger, ex);
            throw;
        }
    }

    /// <summary>
    /// This method is called when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IHostedAmqpConsumer consumer in this.internalConsumers.NewReverseList())
        {
            await consumer.StopAsync(cancellationToken).ConfigureAwait(true);
        }

        this.IsReadOnly = false;
    }

    /// <summary>
    /// Disposes the consumer server asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!this.disposedValue)
        {
            foreach (IHostedAmqpConsumer consumer in this.internalConsumers.NewReverseList())
            {
                await consumer.DisposeAsync().ConfigureAwait(true);
            }
            this.internalConsumers.Clear();
            this.disposedValue = true;
        }
        GC.SuppressFinalize(this);
    }

}

