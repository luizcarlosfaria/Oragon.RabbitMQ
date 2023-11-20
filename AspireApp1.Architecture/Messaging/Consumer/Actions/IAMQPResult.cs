using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AspireApp1.Architecture.Messaging.Consumer.Actions;


public interface IAMQPResult
{
    void Execute(IModel model, BasicDeliverEventArgs delivery);
}
