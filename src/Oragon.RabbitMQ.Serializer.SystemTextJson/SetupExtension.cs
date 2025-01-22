// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Serialization;

namespace Oragon.RabbitMQ;


/// <summary>
/// Configure the services for the Oragon.RabbitMQ.Serializer.NewtonsoftJson
/// </summary>
public static class SetupExtension
{
    /// <summary>
    /// Add the NewtonsoftAmqpSerializer to the services
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    /// <param name="key"></param>
    public static IServiceCollection AddAmqpSerializer(this IServiceCollection services, string key = null, JsonSerializerOptions options = null)
        => AddSystemTextJsonAmqpSerializer(services, key, options);

    /// <summary>
    /// Add the NewtonsoftAmqpSerializer to the services
    /// </summary>
    /// <param name="services"></param>
    /// <param name="key"></param>
    /// <param name="options"></param>
    public static IServiceCollection AddSystemTextJsonAmqpSerializer(this IServiceCollection services, string key = null, JsonSerializerOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return string.IsNullOrWhiteSpace(key)
            ? services.AddSingleton<IAmqpSerializer>(sp => new SystemTextJsonAmqpSerializer(options))
            : services.AddKeyedSingleton<IAmqpSerializer>(key, (key, sp) => new SystemTextJsonAmqpSerializer(options));
    }
}

