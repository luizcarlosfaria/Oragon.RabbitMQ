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
public sealed class FromServicesAttribute : Attribute, IAmqpArgumentBinderParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FromServicesAttribute"/> class.
    /// </summary>
    /// <param name="serviceKey">key for Keyed Service</param>
    public FromServicesAttribute(string serviceKey)
    {
        this.ServiceKey = serviceKey;
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="FromServicesAttribute"/> class.
    /// </summary>
    public FromServicesAttribute() : this(null) { }

    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceKey { get; }

    /// <summary>
    /// Builds the argument binder.
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public IAmqpArgumentBinder Build(ParameterInfo parameter)
    {
        _ = Guard.Argument(parameter).NotNull();

        return string.IsNullOrWhiteSpace(this.ServiceKey)
            ? new FromServicesArgumentBinder(parameter.ParameterType)
            : new FromServicesArgumentBinder(parameter.ParameterType, this.ServiceKey);
    }
}
