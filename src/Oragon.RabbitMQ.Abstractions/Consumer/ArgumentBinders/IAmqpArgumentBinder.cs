// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.


// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.ArgumentBinders;

/// <summary>
/// Represents an argument binder for an Amqp message.
/// </summary>
public interface IAmqpArgumentBinder
{
    /// <summary>
    /// Gets the value of the argument.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    object GetValue(IAmqpContext context);
}
