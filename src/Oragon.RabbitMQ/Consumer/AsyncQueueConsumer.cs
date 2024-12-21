// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
//using OpenTelemetry;
//using OpenTelemetry.Context.Propagation;
using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;



namespace Oragon.RabbitMQ.Consumer;



/// <summary>
/// Represents an asynchronous queue Consumer.
/// </summary>
/// <typeparam name="TService">The type of the service.</typeparam>
/// <typeparam name="TMessage">The type of the incomingMessage.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class AsyncQueueConsumer<TService, TMessage, TResponse> : ConsumerBase
    where TResponse : Task
    where TMessage : class
{
    /// <summary>
    /// The parameters for the Consumer.
    /// </summary>
    private readonly AsyncQueueConsumerParameters<TService, TMessage, TResponse> parameters;

    /// <summary>
    /// The activity source for telemetry.
    /// </summary>
    protected static readonly ActivitySource activitySource = new(MessagingTelemetryNames.GetName(nameof(AsyncQueueConsumer<TService, TMessage, TResponse>)));

    ///// <summary>
    ///// The propagator for trace context.
    ///// </summary>
    //private static readonly TextMapPropagator s_propagator = Propagators.DefaultTextMapPropagator;

    #region Constructors 

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncQueueConsumer{TService, TMessage, TResponse}"/> class.
    /// </summary>
    /// <param name="logger">The Logger.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public AsyncQueueConsumer(ILogger logger, AsyncQueueConsumerParameters<TService, TMessage, TResponse> parameters, IServiceProvider serviceProvider)
        : base(logger, parameters, serviceProvider)
    {
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
    }

    #endregion

    /// <inheritdoc/>
    public override void Validate()
    {
        base.Validate();

        try
        {
            if (this.parameters.DispatchScope == DispatchScope.RootScope)
            {
                _ = this.parameters.GetServiceFunc(this.parameters.ServiceProvider);
            }
            else if (this.parameters.DispatchScope == DispatchScope.ChildScope)
            {
                using var scope = this.parameters.ServiceProvider.CreateScope();

                _ = this.parameters.GetServiceFunc(scope.ServiceProvider);
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

        consumer.ReceivedAsync += this.ReceiveAsync;

        return consumer;
    }


    /// <summary>
    /// Handles the asynchronous receive of a incomingMessage.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="delivery">The delivery arguments.</param>
    [SuppressMessage("Performance", "CA1859", Justification = "Utilização de IAMQPResult é necessária pois ou temos uma resposta do DispatchAsync que pode ser de vários tipos, ou temos uma resposta com RejectResult")]
    public async Task ReceiveAsync(object sender, BasicDeliverEventArgs delivery)
    {
        _ = Guard.Argument(delivery).NotNull();

        using var receiveActivity = new Activity("?AsyncQueueConsumer.ReceiveAsync");

        var result = this.TryDeserialize(receiveActivity, delivery, out var request)
                            ? await this.DispatchAsync(receiveActivity, delivery, request).ConfigureAwait(true)
                            : new RejectResult(false);

        await result.ExecuteAsync(this.Channel, delivery).ConfigureAwait(true);

    }

    private static readonly Action<ILogger, Exception, Exception> s_logErrorOnDesserialize = LoggerMessage.Define<Exception>(LogLevel.Error, new EventId(1, "Message rejected during deserialization"), "Message rejected during deserialization {ExceptionDetails}");


    /// <summary>
    /// Tries to deserialize the received item.
    /// </summary>
    /// <param name="receiveActivity">The receive activity.</param>
    /// <param name="receivedItem">The received item.</param>
    /// <param name="incomingMessage">The deserialized incomingMessage.</param>
    /// <returns><c>true</c> if deserialization is successful; otherwise, <c>false</c>.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceçào global, isolando uma micro-operação")]
    private bool TryDeserialize(Activity receiveActivity, BasicDeliverEventArgs receivedItem, out TMessage incomingMessage)
    {
        _ = Guard.Argument(receivedItem).NotNull();
        _ = Guard.Argument(receiveActivity).NotNull();

        var returnValue = true;

        incomingMessage = default;
        try
        {
            incomingMessage = this.parameters.Serializer.Deserialize<TMessage>(basicDeliver: receivedItem);

            //_ = receiveActivity?.SetTag("incomingMessage", incomingMessage);
        }
        catch (Exception exception)
        {
            returnValue = false;

            //_ = receiveActivity.SetStatus(ActivityStatusCode.Error, exception.ToString());

            s_logErrorOnDesserialize(this.Logger, exception, exception);
        }

        return returnValue;
    }

    private static readonly Action<ILogger, string, Exception, Exception> s_logErrorOnDispatch = LoggerMessage.Define<string, Exception>(LogLevel.Error, new EventId(1, "Exception on processing incomingMessage"), "Exception on processing incomingMessage {QueueName} {Exception}");


    /// <summary>
    /// Dispatches the incomingMessage to the appropriate handler.
    /// </summary>
    /// <param name="receiveActivity">The receive activity.</param>
    /// <param name="receivedItem">The received item.</param>
    /// <param name="incomingMessage">The incomingMessage.</param>
    /// <returns>The result of the dispatch.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceção global, isolando uma macro-operação")]
    [SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
    protected virtual async Task<IAMQPResult> DispatchAsync(Activity receiveActivity, BasicDeliverEventArgs receivedItem, TMessage incomingMessage)
    {
        _ = Guard.Argument(receiveActivity).NotNull();
        _ = Guard.Argument(receivedItem).NotNull();

        if (incomingMessage == null) return new RejectResult(false);

        IAMQPResult returnValue;

        //using var dispatchActivity = activitySource.StartActivity(this.parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context);

        //using (var logContext = new EnterpriseApplicationLogContext())
        //{
        try
        {
            if (this.parameters.DispatchScope == DispatchScope.RootScope)
            {
                var service = this.parameters.GetServiceFunc(this.parameters.ServiceProvider);

                await this.parameters.AdapterFunc(service, incomingMessage).ConfigureAwait(true);
            }
            else if (this.parameters.DispatchScope == DispatchScope.ChildScope)
            {
                using (var scope = this.parameters.ServiceProvider.CreateScope())
                {
                    var service = this.parameters.GetServiceFunc(scope.ServiceProvider);

                    await this.parameters.AdapterFunc(service, incomingMessage).ConfigureAwait(true);
                }
            }
            returnValue = new AckResult();
        }
        catch (Exception exception)
        {
            s_logErrorOnDispatch(this.Logger, this.parameters.QueueName, exception, exception);

            returnValue = new NackResult(this.parameters.RequeueOnCrash);

            //_ = (dispatchActivity?.SetStatus(ActivityStatusCode.Error, exception.ToString()));
        }
        //}

        return returnValue;
    }
}
