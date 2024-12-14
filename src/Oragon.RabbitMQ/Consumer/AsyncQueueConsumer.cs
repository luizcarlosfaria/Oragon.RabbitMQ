// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;



namespace Oragon.RabbitMQ.Consumer;



/// <summary>
/// Represents an asynchronous queue Consumer.
/// </summary>
/// <typeparam name="TService">The type of the service.</typeparam>
/// <typeparam name="TRequest">The type of the messsage.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class AsyncQueueConsumer<TService, TRequest, TResponse> : ConsumerBase
    where TResponse : Task
    where TRequest : class
{
    /// <summary>
    /// The parameters for the Consumer.
    /// </summary>
    private readonly AsyncQueueConsumerParameters<TService, TRequest, TResponse> parameters;

    /// <summary>
    /// The activity source for telemetry.
    /// </summary>
    protected static readonly ActivitySource activitySource = new(MessagingTelemetryNames.GetName(nameof(AsyncQueueConsumer<TService, TRequest, TResponse>)));

    /// <summary>
    /// The propagator for trace context.
    /// </summary>
    private static readonly TextMapPropagator s_propagator = Propagators.DefaultTextMapPropagator;

    #region Constructors 

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncQueueConsumer{TService, TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger">The Logger.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public AsyncQueueConsumer(ILogger logger, AsyncQueueConsumerParameters<TService, TRequest, TResponse> parameters, IServiceProvider serviceProvider)
        : base(logger, parameters, serviceProvider)
    {
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
    }

    #endregion

    /// <inheritdoc/>
    protected override void Validate()
    {
        base.Validate();

        try
        {
            if (this.parameters.DispatchScope == DispatchScope.RootScope)
            {
                _ = this.GetService<TService>(this.parameters.ServiceProvider);
            }
            else if (this.parameters.DispatchScope == DispatchScope.ChildScope)
            {
                using (var scope = this.parameters.ServiceProvider.CreateScope())
                {
                    _ = this.GetService<TService>(scope.ServiceProvider);
                }
            }
        }
        catch (Exception innerException)
        {
            throw new InvalidOperationException($"Cannot obtain {typeof(TService)} from ServiceProvider with scope {this.parameters.DispatchScope}", innerException);
        }
    }


    /// <inheritdoc/>
    protected override IAsyncBasicConsumer BuildConsumer()
    {
        _ = Guard.Argument(this.Channel).NotNull();

        var consumer = new AsyncEventingBasicConsumer(this.Channel);

        consumer.Received += this.ReceiveAsync;

        return consumer;
    }


    /// <summary>
    /// Handles the asynchronous receive of a message.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="delivery">The delivery arguments.</param>
    [SuppressMessage("Performance", "CA1859", Justification = "Utilização de IAMQPResult é necessária pois ou temos uma resposta do DispatchAsync que pode ser de vários tipos, ou temos uma resposta com RejectResult")]
    public async Task ReceiveAsync(object sender, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(delivery).NotNull();

        var parentContext = s_propagator.Extract(default, delivery.BasicProperties, this.ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        using var receiveActivity = activitySource.StartActivity("AsyncQueueConsumer.ReceiveAsync", ActivityKind.Consumer, parentContext.ActivityContext) ?? new Activity("?AsyncQueueConsumer.ReceiveAsync");

        _ = receiveActivity
            .AddTag("Queue", this.parameters.QueueName)
            .AddTag("MessageId", delivery.BasicProperties.MessageId)
            .AddTag("CorrelationId", delivery.BasicProperties.CorrelationId)
            .SetTag("messaging.system", "rabbitmq")
            .SetTag("messaging.destination_kind", "queue")
            .SetTag("messaging.destination", delivery.Exchange)
            .SetTag("messaging.rabbitmq.routing_key", delivery.RoutingKey);

        var result = this.TryDeserialize(receiveActivity, delivery, out var request)
                            ? await this.DispatchAsync(receiveActivity, delivery, request).ConfigureAwait(true)
                            : new RejectResult(false);

        await result.ExecuteAsync(this.Channel, delivery).ConfigureAwait(true);

        //receiveActivity?.SetEndTime(DateTime.UtcNow);
    }

    private static readonly Action<ILogger, Exception> s_logErrorOnExtractTraceContext = LoggerMessage.Define(LogLevel.Error, new EventId(1, "Failed to extract trace context."), "Failed to extract trace context.");

    /// <summary>
    /// Extracts the trace context from the basic properties.
    /// </summary>
    /// <param name="props">The basic properties.</param>
    /// <param name="key">The key.</param>
    /// <returns>The trace context.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceçào global, isolando uma micro-operação")]
    private IEnumerable<string> ExtractTraceContextFromBasicProperties(IReadOnlyBasicProperties props, string key)
    {
        try
        {
            if (props.Headers != null && props.Headers.TryGetValue(key, out var value) && (value is byte[] bytes))
            {
                return [Encoding.UTF8.GetString(bytes)];
            }
        }
        catch (Exception ex)
        {
            s_logErrorOnExtractTraceContext(this.Logger, ex);
        }

        return [];
    }


    private static readonly Action<ILogger, Exception, Exception> s_logErrorOnDesserialize = LoggerMessage.Define<Exception>(LogLevel.Error, new EventId(1, "Message rejected during deserialization"), "Message rejected during deserialization {ExceptionDetails}");


    /// <summary>
    /// Tries to deserialize the received item.
    /// </summary>
    /// <param name="receiveActivity">The receive activity.</param>
    /// <param name="receivedItem">The received item.</param>
    /// <param name="messsage">The deserialized messsage.</param>
    /// <returns><c>true</c> if deserialization is successful; otherwise, <c>false</c>.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceçào global, isolando uma micro-operação")]
    private bool TryDeserialize(Activity receiveActivity, BasicDeliverEventArgs receivedItem, out TRequest messsage)
    {
        _ = Guard.Argument(receivedItem).NotNull();

        var returnValue = true;

        messsage = default;
        try
        {
            messsage = this.parameters.Serializer.Deserialize<TRequest>(eventArgs: receivedItem);

            _ = receiveActivity?.SetTag("message", messsage);
        }
        catch (Exception exception)
        {
            returnValue = false;

            _ = receiveActivity.SetStatus(ActivityStatusCode.Error, exception.ToString());

            s_logErrorOnDesserialize(this.Logger, exception, exception);
        }

        return returnValue;
    }

    private static readonly Action<ILogger, string, Exception, Exception> s_logErrorOnDispatch = LoggerMessage.Define<string, Exception>(LogLevel.Error, new EventId(1, "Exception on processing message"), "Exception on processing message {QueueName} {Exception}");


    /// <summary>
    /// Dispatches the messsage to the appropriate handler.
    /// </summary>
    /// <param name="receiveActivity">The receive activity.</param>
    /// <param name="receivedItem">The received item.</param>
    /// <param name="request">The messsage.</param>
    /// <returns>The result of the dispatch.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceção global, isolando uma macro-operação")]
    protected virtual async Task<IAMQPResult> DispatchAsync(Activity receiveActivity, BasicDeliverEventArgs receivedItem, TRequest request)
    {
        _ = Guard.Argument(receiveActivity).NotNull();
        _ = Guard.Argument(receivedItem).NotNull();

        if (request == null) return new RejectResult(false);

        IAMQPResult returnValue;

        using var dispatchActivity = activitySource.StartActivity(this.parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context);

        //using (var logContext = new EnterpriseApplicationLogContext())
        //{
        try
        {
            if (this.parameters.DispatchScope == DispatchScope.RootScope)
            {
                var service = this.GetService<TService>(this.parameters.ServiceProvider);

                await this.parameters.AdapterFunc(service, request).ConfigureAwait(true);
            }
            else if (this.parameters.DispatchScope == DispatchScope.ChildScope)
            {
                using (var scope = this.parameters.ServiceProvider.CreateScope())
                {
                    var service = this.GetService<TService>(scope.ServiceProvider);

                    await this.parameters.AdapterFunc(service, request).ConfigureAwait(true);
                }
            }
            returnValue = new AckResult();
        }
        catch (Exception exception)
        {
            s_logErrorOnDispatch(this.Logger, this.parameters.QueueName, exception, exception);

            returnValue = new NackResult(this.parameters.RequeueOnCrash);

            _ = (dispatchActivity?.SetStatus(ActivityStatusCode.Error, exception.ToString()));
        }
        //}

        return returnValue;
    }

    private T GetService<T>(IServiceProvider serviceProvider)
    {
        return this.parameters.IsKeyedService
            ? serviceProvider.GetRequiredKeyedService<T>(this.parameters.KeyOfService)
            : serviceProvider.GetRequiredService<T>();
    }
}
