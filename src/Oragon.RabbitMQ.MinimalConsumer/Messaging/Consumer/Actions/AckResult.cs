using Dawn;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace DotNetAspire.Architecture.Messaging.Consumer.Actions;

public class AckResult : IAMQPResult
{
    public void Execute(IModel model, BasicDeliverEventArgs delivery)
    {
        Guard.Argument(model).NotNull();
        Guard.Argument(delivery).NotNull();

        model.BasicAck(delivery.DeliveryTag, false);
    }
}
