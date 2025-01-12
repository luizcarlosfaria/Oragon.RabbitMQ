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
    /// Add the NewtonsoftAMQPSerializer to the services
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    /// <param name="key"></param>
    public static IServiceCollection AddAMQPSerializer(this IServiceCollection services, string key = null, JsonSerializerOptions options = null)
        => AddSystemTextJsonAMQPSerializer(services, key, options);

    /// <summary>
    /// Add the NewtonsoftAMQPSerializer to the services
    /// </summary>
    /// <param name="services"></param>
    /// <param name="key"></param>
    /// <param name="options"></param>
    public static IServiceCollection AddSystemTextJsonAMQPSerializer(this IServiceCollection services, string key = null, JsonSerializerOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return string.IsNullOrWhiteSpace(key)
            ? services.AddSingleton<IAMQPSerializer>(sp => new SystemTextJsonAMQPSerializer(options))
            : services.AddKeyedSingleton<IAMQPSerializer>(key, (key, sp) => new SystemTextJsonAMQPSerializer(options));
    }
}

