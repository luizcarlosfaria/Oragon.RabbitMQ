// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;

namespace Oragon.RabbitMQ.Consumer.Dispatch.Attributes;

/// <summary>
/// Represents an attribute that can be used to bind an argument to an Amqp message.
/// </summary>
public interface IAmqpArgumentBinderParameter
{
    /// <summary>
    /// Builds the argument binder.
    /// </summary>
    /// <returns></returns>
    IAmqpArgumentBinder Build(ParameterInfo parameter);
}
