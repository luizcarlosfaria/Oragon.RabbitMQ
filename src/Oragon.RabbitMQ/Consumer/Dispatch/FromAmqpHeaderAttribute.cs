// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Dawn;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Represents an attribute that can be used to bind an argument to an AMQP message.
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
        this.Key = Guard.Argument(key).NotNull().NotEmpty().NotWhiteSpace().Value;
    }

    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string Key { get; }

    public IAmqpArgumentBinder Build(ParameterInfo parameter)
    {
        return parameter.ParameterType != Constants.String
            ? throw new InvalidOperationException($"The parameter {parameter.Name} must be of type string")
            : new DynamicArgumentBinder((context) => context.Request.BasicProperties.Headers[this.Key]);
    }
}
