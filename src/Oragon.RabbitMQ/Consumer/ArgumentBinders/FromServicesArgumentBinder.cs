// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;

namespace Oragon.RabbitMQ.Consumer.ArgumentBinders;

/// <summary>
/// Represents an argument binder for an Amqp message.
/// </summary>
/// <param name="parameterType"></param>
/// <param name="serviceKey"></param>
public class FromServicesArgumentBinder(Type parameterType, string serviceKey = null) : IAmqpArgumentBinder
{
    /// <summary>
    /// Get the Service Type used to get a service from dependency injection
    /// </summary>
    public Type ParameterType { get; } = parameterType;

    /// <summary>
    /// Get the Service Key (if needed) used to get a service from dependency injection
    /// </summary>
    public string ServiceKey { get; } = serviceKey;

    /// <summary>
    /// Get value from IAmqpContext
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public object GetValue(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return this.GetValue(context.ServiceProvider);
    }

    /// <summary>
    /// Get value from service provider
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <returns></returns>
    public object GetValue(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));

        return string.IsNullOrWhiteSpace(this.ServiceKey)
            ? serviceProvider.GetRequiredService(this.ParameterType)
            : serviceProvider.GetRequiredKeyedService(this.ParameterType, this.ServiceKey);
    }
}
