using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GagoAspireApp.Architecture.Messaging.Consumer.Actions;


public interface IAMQPResult
{
    void Execute(IModel model, BasicDeliverEventArgs delivery);
}
