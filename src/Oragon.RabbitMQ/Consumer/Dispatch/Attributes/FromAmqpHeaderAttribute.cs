// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using RabbitMQ.Client;

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
    public IAmqpArgumentBinder Build(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return new DynamicArgumentBinder((context) => this.GetValue(context, parameter));
    }

    private object GetValue(IAmqpContext context, ParameterInfo parameter)
    {
        IReadOnlyBasicProperties properties = context.Request.BasicProperties;
        IDictionary<string, object> headers = properties.Headers;
        if (headers == null || !headers.ContainsKey(this.Key))
        {
            Type targetType = parameter.ParameterType;
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
            {
                throw new InvalidOperationException($"Required AMQP header '{this.Key}' for parameter '{parameter.Name}' is missing.");
            }

            return null;
        }

        return AmqpHeaders.Get(properties, this.Key, parameter.ParameterType);
    }
}
