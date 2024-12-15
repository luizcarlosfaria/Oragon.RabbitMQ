// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Consumer;


/// <summary>
/// A consumer that processes messages in an RPC flow.
/// </summary>
/// <typeparam name="TService"></typeparam>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class AsyncRpcConsumer<TService, TRequest, TResponse> : AsyncQueueConsumer<TService, TRequest, Task<TResponse>>
    where TResponse : class
    where TRequest : class
{
    private readonly AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>> parameters;

    /// <summary>
    /// Creates a new instance of the <see cref="AsyncRpcConsumer{TService, TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="parameters"></param>
    /// <param name="serviceProvider"></param>
    public AsyncRpcConsumer(ILogger logger, AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>> parameters, IServiceProvider serviceProvider)
        : base(logger, parameters, serviceProvider)
    {
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
    }

    private static readonly Action<ILogger, Exception> s_logErrorOnDispatchWithoutReplyTo = LoggerMessage.Define(LogLevel.Error, new EventId(1, "Message cannot be processed in RPC Flow because original message didn't have a ReplyTo."), "Message cannot be processed in RPC Flow because original message didn't have a ReplyTo.");


    /// <summary>
    /// Dispatches the message to the service.
    /// </summary>
    /// <param name="receiveActivity"></param>
    /// <param name="receivedItem"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceçào global, isolando uma MACRO-operação")]
    protected override async Task<IAMQPResult> DispatchAsync(Activity receiveActivity, BasicDeliverEventArgs receivedItem, TRequest request)
    {
        _ = Guard.Argument(receivedItem).NotNull();
        _ = Guard.Argument(receiveActivity).NotNull();
        _ = Guard.Argument(request).NotNull();

        if (receivedItem.BasicProperties.ReplyTo == null)
        {
            s_logErrorOnDispatchWithoutReplyTo(this.Logger, null);

            return new RejectResult(false);
        }

        TResponse responsePayload = default;

        using (var dispatchActivity = activitySource.StartActivity(this.parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context))
        {
            try
            {
                if (this.parameters.DispatchScope == DispatchScope.RootScope)
                {
                    var service = this.parameters.GetServiceFunc(this.parameters.ServiceProvider);

                    responsePayload = await this.parameters.AdapterFunc(service, request).ConfigureAwait(true);
                }
                else if (this.parameters.DispatchScope == DispatchScope.ChildScope)
                {
                    using (var scope = this.parameters.ServiceProvider.CreateScope())
                    {
                        var service = this.parameters.GetServiceFunc(scope.ServiceProvider);

                        responsePayload = await this.parameters.AdapterFunc(service, request).ConfigureAwait(true);
                    }
                }
            }
            catch (Exception exception)
            {
                _ = (dispatchActivity?.SetStatus(ActivityStatusCode.Error, exception.ToString()));

                await this.SendReplyAsync(dispatchActivity, receivedItem, null, exception).ConfigureAwait(true);

                return new NackResult(this.parameters.RequeueOnCrash);
            }
        }

        using (var replyActivity = activitySource.StartActivity(this.parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context))
        {
            await this.SendReplyAsync(replyActivity, receivedItem, responsePayload).ConfigureAwait(true);
        }
        return new AckResult();
    }

    private async Task SendReplyAsync(Activity activity, BasicDeliverEventArgs receivedItem, TResponse responsePayload = null, Exception exception = null)
    {
        _ = Guard.Argument(receivedItem).NotNull();
        _ = Guard.Argument(receivedItem.BasicProperties).NotNull();
        _ = Guard.Argument(responsePayload).NotNull();


        var responseProperties = this.Channel.CreateBasicProperties()
                                                        .SetMessageId()
                                                        .TrySetException(exception)
                                                        .SetTelemetry(activity)
                                                        .SetCorrelationId(receivedItem.BasicProperties);

        _ = (activity?.AddTag("Queue", receivedItem.BasicProperties.ReplyTo));
        _ = (activity?.AddTag("MessageId", responseProperties.MessageId));
        _ = (activity?.AddTag("CorrelationId", responseProperties.CorrelationId));

        await this.Channel.BasicPublishAsync(
            string.Empty,
            receivedItem.BasicProperties.ReplyTo,
            false,
            responseProperties,
            exception != null
                ? []
                : this.parameters.Serializer.Serialize(basicProperties: responseProperties, message: responsePayload)
        ).ConfigureAwait(true);

        //replyActivity?.SetEndTime(DateTime.UtcNow);
    }

}

