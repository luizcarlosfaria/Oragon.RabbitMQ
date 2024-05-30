using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Text;
using Oragon.RabbitMQ.Consumer.Actions;
using System.Diagnostics.CodeAnalysis;


namespace Oragon.RabbitMQ.Consumer;



/// <summary>
/// Represents an asynchronous queue Consumer.
/// </summary>
/// <typeparam name="TService">The type of the service.</typeparam>
/// <typeparam name="TRequest">The type of the request.</typeparam>
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
    protected override IBasicConsumer BuildConsumer()
    {
        _ = Guard.Argument(Channel).NotNull();

        var consumer = new AsyncEventingBasicConsumer(Channel);

        consumer.Received += ReceiveAsync;

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

        var parentContext = s_propagator.Extract(default, (IReadOnlyBasicProperties)delivery.BasicProperties, this.ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        using var receiveActivity = activitySource.StartActivity("AsyncQueueConsumer.ReceiveAsync", ActivityKind.Consumer, parentContext.ActivityContext) ?? new Activity("?AsyncQueueConsumer.ReceiveAsync");

        _ = receiveActivity.AddTag("Queue", parameters.QueueName);
        _ = receiveActivity.AddTag("MessageId", delivery.BasicProperties.MessageId);
        _ = receiveActivity.AddTag("CorrelationId", delivery.BasicProperties.CorrelationId);

        _ = receiveActivity.SetTag("messaging.system", "rabbitmq");
        _ = receiveActivity.SetTag("messaging.destination_kind", "queue");
        _ = receiveActivity.SetTag("messaging.destination", delivery.Exchange);
        _ = receiveActivity.SetTag("messaging.rabbitmq.routing_key", delivery.RoutingKey);

        IAMQPResult result = TryDeserialize(receiveActivity, delivery, out var request)
                            ? await DispatchAsync(receiveActivity, delivery, request).ConfigureAwait(true)
                            : (IAMQPResult)new RejectResult(false);

        await result.ExecuteAsync(Channel, delivery).ConfigureAwait(true);

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
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return new[] { Encoding.UTF8.GetString(bytes) };
            }
        }
        catch (Exception ex)
        {
            s_logErrorOnExtractTraceContext(Logger, ex);
        }

        return Enumerable.Empty<string>();
    }


    private static readonly Action<ILogger, Exception, Exception> s_logErrorOnDesserialize = LoggerMessage.Define<Exception>(LogLevel.Error, new EventId(1, "Message rejected during deserialization"), "Message rejected during deserialization {ExceptionDetails}");

    
    /// <summary>
    /// Tries to deserialize the received item.
    /// </summary>
    /// <param name="receiveActivity">The receive activity.</param>
    /// <param name="receivedItem">The received item.</param>
    /// <param name="request">The deserialized request.</param>
    /// <returns><c>true</c> if deserialization is successful; otherwise, <c>false</c>.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceçào global, isolando uma micro-operação")]
    private bool TryDeserialize(Activity receiveActivity, BasicDeliverEventArgs receivedItem, out TRequest request)
    {
        _ = Guard.Argument(receivedItem).NotNull();

        var returnValue = true;

        request = default;
        try
        {
            request = parameters.Serializer.Deserialize<TRequest>(eventArgs: receivedItem);
        }
        catch (Exception exception)
        {
            returnValue = false;

            _ = receiveActivity.SetStatus(ActivityStatusCode.Error, exception.ToString());

            s_logErrorOnDesserialize(Logger, exception, exception);
        }

        return returnValue;
    }

    private static readonly Action<ILogger, string, Exception, Exception> s_logErrorOnDispatch = LoggerMessage.Define<string, Exception>(LogLevel.Error, new EventId(1, "Exception on processing message"), "Exception on processing message {QueueName} {Exception}");


    /// <summary>
    /// Dispatches the request to the appropriate handler.
    /// </summary>
    /// <param name="receiveActivity">The receive activity.</param>
    /// <param name="receivedItem">The received item.</param>
    /// <param name="request">The request.</param>
    /// <returns>The result of the dispatch.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceção global, isolando uma macro-operação")]
    protected virtual async Task<IAMQPResult> DispatchAsync(Activity receiveActivity, BasicDeliverEventArgs receivedItem, TRequest request)
    {
        _ = Guard.Argument(receiveActivity).NotNull();
        _ = Guard.Argument(receivedItem).NotNull();

        if (request == null) return new RejectResult(false);

        IAMQPResult returnValue;

        using var dispatchActivity = activitySource.StartActivity(parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context);

        //using (var logContext = new EnterpriseApplicationLogContext())
        //{
        try
        {
            var service = parameters.ServiceProvider.GetRequiredService<TService>();

            if (parameters.DispatchScope == DispatchScope.RootScope)
            {
                await parameters.AdapterFunc(service, request);
            }
            else if (parameters.DispatchScope == DispatchScope.ChildScope)
            {
                using (var scope = parameters.ServiceProvider.CreateScope())
                {
                    await parameters.AdapterFunc(service, request);
                }
            }
            returnValue = new AckResult();
        }
        catch (Exception exception)
        {
            s_logErrorOnDispatch(Logger, parameters.QueueName, exception, exception);

            returnValue = new NackResult(parameters.RequeueOnCrash);

            _ = (dispatchActivity?.SetStatus(ActivityStatusCode.Error, exception.ToString()));
        }
        //}

        return returnValue;
    }
}
