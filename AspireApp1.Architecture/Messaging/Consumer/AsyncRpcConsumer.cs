using AspireApp1.Architecture.Messaging;
using AspireApp1.Architecture.Messaging.Consumer.Actions;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;

namespace AspireApp1.Architecture.Messaging.Consumer;


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

    protected override async Task<IAMQPResult> Dispatch(BasicDeliverEventArgs receivedItem, Activity receiveActivity, TRequest request)
    {
        Guard.Argument(receivedItem).NotNull();
        Guard.Argument(receiveActivity).NotNull();
        Guard.Argument(request).NotNull();

        using Activity dispatchActivity = this.parameters.ActivitySource.SafeStartActivity($"{nameof(AsyncRpcConsumer<TService, TRequest, TResponse>)}.Dispatch", ActivityKind.Internal, receiveActivity.Context);

        if (receivedItem.BasicProperties.ReplyTo == null)
        {
            this.logger.LogWarning("Message cannot be processed in RPC Flow because original message didn't have a ReplyTo.");

            return new RejectResult(false);
        }

        TResponse responsePayload = default;

        try
        {
            if (this.parameters.DispatchScope == DispatchScope.RootScope)
                responsePayload = await this.parameters.AdapterFunc(this.parameters.ServiceProvider.GetRequiredService<TService>(), request);
            else if (this.parameters.DispatchScope == DispatchScope.ChildScope)
            {
                using (var scope = this.parameters.ServiceProvider.CreateScope())
                {
                    responsePayload = await this.parameters.AdapterFunc(scope.ServiceProvider.GetRequiredService<TService>(), request);
                }
            }
        }
        catch (Exception ex)
        {
            this.SendReply(receivedItem, receiveActivity, null, ex);

            return new NackResult(this.parameters.RequeueOnCrash);
        }

        dispatchActivity?.SetEndTime(DateTime.UtcNow);

        this.SendReply(receivedItem, receiveActivity, responsePayload);

        return new AckResult();
    }

    private void SendReply(BasicDeliverEventArgs receivedItem, Activity receiveActivity, TResponse responsePayload = null, Exception exception = null)
    {
        Guard.Argument(receivedItem).NotNull();
        Guard.Argument(receiveActivity).NotNull();
        Guard.Argument(responsePayload).NotNull();

        using Activity replyActivity = this.parameters.ActivitySource.SafeStartActivity($"{nameof(AsyncRpcConsumer<TService, TRequest, TResponse>)}.Reply", ActivityKind.Client, receiveActivity.Context);


        IBasicProperties responseProperties = this.Model.CreateBasicProperties()
                                                        .SetMessageId()
                                                        .IfFunction(it => exception != null, it => it.SetException(exception))
                                                        .SetTelemetry(replyActivity)
                                                        .SetCorrelationId(receivedItem.BasicProperties);

        replyActivity?.AddTag("Queue", receivedItem.BasicProperties.ReplyTo);
        replyActivity?.AddTag("MessageId", responseProperties.MessageId);
        replyActivity?.AddTag("CorrelationId", responseProperties.CorrelationId);

        this.Model.BasicPublish(string.Empty,
            receivedItem.BasicProperties.ReplyTo,
            responseProperties,
            exception != null
                ? Array.Empty<byte>()
                : this.parameters.Serializer.Serialize(responseProperties, responsePayload)
        );

        replyActivity?.SetEndTime(DateTime.UtcNow);
    }

}
