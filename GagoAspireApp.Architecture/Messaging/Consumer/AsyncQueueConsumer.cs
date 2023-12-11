using GagoAspireApp.Architecture.Messaging;
using GagoAspireApp.Architecture.Messaging.Consumer.Actions;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;

namespace GagoAspireApp.Architecture.Messaging.Consumer;


public class AsyncQueueConsumer<TService, TRequest, TResponse> : ConsumerBase
    where TResponse : Task
    where TRequest : class
{
    private AsyncQueueConsumerParameters<TService, TRequest, TResponse> parameters;

    #region Constructors 

    public AsyncQueueConsumer(ILogger logger, AsyncQueueConsumerParameters<TService, TRequest, TResponse> parameters, IServiceProvider serviceProvider)
        : base(logger, parameters, serviceProvider)
    {
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
    }

    #endregion


    protected override IBasicConsumer BuildConsumer()
    {
        Guard.Argument(this.Model).NotNull();

        var consumer = new AsyncEventingBasicConsumer(this.Model);

        consumer.Received += this.Receive;

        return consumer;
    }

    public async Task Receive(object sender, BasicDeliverEventArgs delivery)
    {
        Guard.Argument(delivery).NotNull();
        Guard.Argument(delivery.BasicProperties).NotNull();

        using Activity receiveActivity = this.parameters.ActivitySource.SafeStartActivity("AsyncQueueServiceWorker.Receive", ActivityKind.Consumer);
        receiveActivity.SetParentId(delivery.BasicProperties.GetTraceId(), delivery.BasicProperties.GetSpanId());
        receiveActivity.AddTag("Queue", this.parameters.QueueName);
        receiveActivity.AddTag("MessageId", delivery.BasicProperties.MessageId);
        receiveActivity.AddTag("CorrelationId", delivery.BasicProperties.CorrelationId);

        IAMQPResult result = this.TryDeserialize(delivery, out TRequest request)
                            ? await this.Dispatch(delivery, receiveActivity, request)
                            : new RejectResult(false);

        result.Execute(this.Model, delivery);

        receiveActivity?.SetEndTime(DateTime.UtcNow);
    }

    private bool TryDeserialize(BasicDeliverEventArgs receivedItem, out TRequest request)
    {
        Guard.Argument(receivedItem).NotNull();

        bool returnValue  = true;

        request = default;
        try
        {
            request = this.parameters.Serializer.Deserialize<TRequest>(receivedItem);
        }
        catch (Exception exception)
        {
            returnValue = false;

            this.logger.LogWarning("Message rejected during deserialization {exception}", exception);
        }

        return returnValue;
    }

    protected virtual async Task<IAMQPResult> Dispatch(BasicDeliverEventArgs receivedItem, Activity receiveActivity, TRequest request)
    {
        Guard.Argument(receivedItem).NotNull();
        Guard.Argument(receiveActivity).NotNull();
        if (request == null) return new RejectResult(false);

        IAMQPResult returnValue;

        using Activity dispatchActivity = this.parameters.ActivitySource.SafeStartActivity("AsyncQueueServiceWorker.Dispatch", ActivityKind.Internal, receiveActivity.Context);

        //using (var logContext = new EnterpriseApplicationLogContext())
        //{
            try
            {
                TService service = this.parameters.ServiceProvider.GetRequiredService<TService>();

                if (this.parameters.DispatchScope == DispatchScope.RootScope)
                    await this.parameters.AdapterFunc(service, request);
                else if (this.parameters.DispatchScope == DispatchScope.ChildScope)
                {
                    using (var scope = this.parameters.ServiceProvider.CreateScope())
                    {
                        await this.parameters.AdapterFunc(service, request);
                    }
                }
                returnValue = new AckResult();
            }
            catch (Exception exception)
            {
                //logContext?.Exception = exception;
                this.logger.LogWarning("Exception on processing message {queueName} {exception}", this.parameters.QueueName, exception);
                returnValue = new NackResult(this.parameters.RequeueOnCrash);
            }
        //}

        dispatchActivity?.SetEndTime(DateTime.UtcNow);

        return returnValue;
    }
}