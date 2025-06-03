// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;

namespace Oragon.RabbitMQ.Consumer.Dispatch.Attributes;

/// <summary>
/// Represents an attribute that can be used to bind an argument to an Amqp message.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromAmqpHeaderAttribute : Attribute, IAmqpArgumentBinderParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FromServicesAttribute"/> class.
    /// </summary>
    /// <param name="key">key for Keyed Service</param>
    public FromAmqpHeaderAttribute(string key)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(key);
        this.Key = key;
    }

    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string Key { get; }


    /// <summary>
    /// Builds the argument binder.
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public IAmqpArgumentBinder Build(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return parameter.ParameterType != Constants.String
            ? throw new InvalidOperationException($"The parameter {parameter.Name} must be of type string")
            : new DynamicArgumentBinder((context) => context.Request.BasicProperties.Headers?[this.Key] ?? null);
    }
}
