using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace GagoAspireApp.Architecture.Messaging.Consumer.Actions;

public class NackResult : IAMQPResult
{
    public bool Requeue { get; }

    public NackResult(bool requeue)
    {
        this.Requeue = requeue;
    }

    public void Execute(IModel model, BasicDeliverEventArgs delivery)
    {
        Guard.Argument(model).NotNull();
        Guard.Argument(delivery).NotNull();

        model.BasicNack(delivery.DeliveryTag, false, this.Requeue);
    }
}
