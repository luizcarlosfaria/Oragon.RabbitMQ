// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Dawn;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;

namespace Oragon.RabbitMQ.Consumer.Dispatch.Attributes;

/// <summary>
/// Represents an attribute that can be used to bind an argument to an Amqp message.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromBodyAttribute : Attribute, IAmqpArgumentBinderParameter
{
    /// <summary>
    /// Builds the argument binder.
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public IAmqpArgumentBinder Build(ParameterInfo parameter)
    {
        _ = Guard.Argument(parameter).NotNull();

        return new MessageObjectArgumentBinder(parameter.ParameterType);
    }
}
