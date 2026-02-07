// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ;

/// <summary>
/// Extensions for Telemetry
/// </summary>
public static class TelemetryExtensions
{

    /// <summary>
    /// Validate if the BasicProperties has Headers, if not, create a new Dictionary
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <returns></returns>
    public static BasicProperties EnsureHeaders(this BasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        basicProperties.Headers ??= new Dictionary<string, object>();
        return basicProperties;
    }

    
    private static BasicProperties SetHeader(this BasicProperties basicProperties, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);

        basicProperties = basicProperties.EnsureHeaders();

        basicProperties.Headers![key] = value;

        return basicProperties;
    }

}
