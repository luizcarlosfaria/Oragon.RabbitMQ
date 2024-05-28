using DotNetAspire.Architecture.Messaging.Consumer.Actions;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer;


public class AsyncRpcConsumer<TService, TRequest, TResponse> : AsyncQueueConsumer<TService, TRequest, Task<TResponse>>
    where TResponse : class
    where TRequest : class
{
    private AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>> parameters;

    public AsyncRpcConsumer(ILogger logger, AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>> parameters, IServiceProvider serviceProvider)
        : base(logger, parameters, serviceProvider)
    {
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
    }

    protected override async Task<IAMQPResult> Dispatch(Activity receiveActivity, BasicDeliverEventArgs receivedItem, TRequest request)
    {
        Guard.Argument(receivedItem).NotNull();
        Guard.Argument(receiveActivity).NotNull();
        Guard.Argument(request).NotNull();

        if (receivedItem.BasicProperties.ReplyTo == null)
        {
            logger.LogWarning("Message cannot be processed in RPC Flow because original message didn't have a ReplyTo.");

            return new RejectResult(false);
        }

        TResponse responsePayload = default;

        using (var dispatchActivity = activitySource.StartActivity(parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context))
        {
            try
            {
                var service = parameters.ServiceProvider.GetRequiredService<TService>();

                if (parameters.DispatchScope == DispatchScope.RootScope)
                {
                    responsePayload = await parameters.AdapterFunc(service, request);
                }
                else if (parameters.DispatchScope == DispatchScope.ChildScope)
                {
                    using (var scope = parameters.ServiceProvider.CreateScope())
                    {
                        responsePayload = await parameters.AdapterFunc(service, request);
                    }
                }
            }
            catch (Exception exception)
            {
                dispatchActivity?.SetStatus(ActivityStatusCode.Error, exception.ToString());

                SendReply(dispatchActivity, receivedItem, null, exception);

                return new NackResult(parameters.RequeueOnCrash);
            }
        }

        using (var replyActivity = activitySource.StartActivity(parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context))
        {
            SendReply(replyActivity, receivedItem, responsePayload);
        }
        return new AckResult();
    }

    private void SendReply(Activity activity, BasicDeliverEventArgs receivedItem, TResponse responsePayload = null, Exception exception = null)
    {
        Guard.Argument(receivedItem).NotNull();
        Guard.Argument(responsePayload).NotNull();


        var responseProperties = Model.CreateBasicProperties()
                                                        .SetMessageId()
                                                        .IfFunction(it => exception != null, it => it.SetException(exception))
                                                        .SetTelemetry(activity)
                                                        .SetCorrelationId(receivedItem.BasicProperties);

        activity?.AddTag("Queue", receivedItem.BasicProperties.ReplyTo);
        activity?.AddTag("MessageId", responseProperties.MessageId);
        activity?.AddTag("CorrelationId", responseProperties.CorrelationId);

        Model.BasicPublish(string.Empty,
            receivedItem.BasicProperties.ReplyTo,
            responseProperties,
            exception != null
                ? Array.Empty<byte>()
                : parameters.Serializer.Serialize(basicProperties: responseProperties, objectToSerialize: responsePayload)
        );

        //replyActivity?.SetEndTime(DateTime.UtcNow);
    }

}
