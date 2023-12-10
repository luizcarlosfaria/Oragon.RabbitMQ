using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Polly;

namespace GagoAspireApp.Architecture.Messaging.Consumer.Actions;

public class RejectResult : IAMQPResult
{
    public bool Requeue { get; }

    public RejectResult(bool requeue)
    {
        this.Requeue = requeue;
    }

    public void Execute(IModel model, BasicDeliverEventArgs delivery)
    {
        Guard.Argument(model).NotNull();
        Guard.Argument(delivery).NotNull();

        model.BasicReject(delivery.DeliveryTag, this.Requeue);
    }
}
