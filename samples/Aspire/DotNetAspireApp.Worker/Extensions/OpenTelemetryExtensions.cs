// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Collections;

namespace DotNetAspireApp.Worker.Extensions;

public static class OpenTelemetryExtensions
{
    public static string? GetOtelResourceAttributes()
    {
        return Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES");
    }

    public static IDictionary<string, string> GetOtelResourceAttributesDictionary()
    {
        string? otelAttrs = GetOtelResourceAttributes();
        if (string.IsNullOrEmpty(otelAttrs))
        {
            return new Dictionary<string, string>();
        }

        return otelAttrs.Split(',')
                        .Select(part => part.Split('='))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }

    public static string? GetOtelResourceAttributesVariable(string key)
    {
        IDictionary<string, string> dictionary = GetOtelResourceAttributesDictionary();
        if (dictionary != null && dictionary.TryGetValue(key, out string? value))
            return value;

        return default;
    }

    public static string? GetOtelInstanceId()
        => GetOtelResourceAttributesVariable("service.instance.id");

    public static string? GetOtelServiceName()
        => Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? GetOtelResourceAttributesVariable("service.name");
}
