// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Represents an attribute that can be used to bind an argument to an AMQP message.
/// </summary>
public class MessageObjectArgumentBinder : IAmqpArgumentBinder
{
    public MessageObjectArgumentBinder(Type type)
    {
        this.Type = type;
    }

    public Type Type { get; }

    public object GetValue(IAmqpContext context)
    {
        _ = Guard.Argument(context).NotNull();

        return context.MessageObject;
    }
}
