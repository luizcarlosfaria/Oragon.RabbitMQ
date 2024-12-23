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
    /// StartActivity with the given name and kind, or return a new Activity with the name prefixed with "?".
    /// </summary>
    /// <param name="activitySource"></param>
    /// <param name="name"></param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static Activity SafeStartActivity(this ActivitySource activitySource, ActivityKind kind = ActivityKind.Internal, [CallerMemberName] string name = "")
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        Activity activity = activitySource.StartActivity(name, kind) ?? new Activity(name);
        _ = activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    /// <summary>
    /// StartActivity with the given name and kind, or return a new Activity with the name prefixed with "?".
    /// </summary>
    /// <param name="activitySource"></param>
    /// <param name="name"></param>
    /// <param name="kind"></param>
    /// <param name="parentContext"></param>
    /// <returns></returns>
    public static Activity SafeStartActivity(this ActivitySource activitySource, ActivityKind kind, ActivityContext parentContext, [CallerMemberName] string name = "")
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        Activity activity = activitySource.StartActivity(name, kind, parentContext) ?? new Activity(name);
        _ = activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    /// <summary>
    /// Get the TraceId from the BasicProperties
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <returns></returns>
    public static ActivityTraceId GetTraceId(this BasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        return basicProperties.Headers != null && basicProperties.Headers.ContainsKey("TraceId")
            ? ActivityTraceId.CreateFromString(basicProperties.Headers.AsString("TraceId"))
            : default;
    }

    /// <summary>
    /// Get the SpanId from the BasicProperties
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <returns></returns>
    public static ActivitySpanId GetSpanId(this BasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        return basicProperties.Headers != null && basicProperties.Headers.ContainsKey("SpanId")
            ? ActivitySpanId.CreateFromString(basicProperties.Headers.AsString("SpanId"))
            : default;
    }

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

    /// <summary>
    /// Set the TraceId and SpanId in the BasicProperties
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="activity"></param>
    /// <returns></returns>
    public static BasicProperties SetTelemetry(this BasicProperties basicProperties, Activity activity)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);

        return (activity != null)
            ? basicProperties
                .SetSpanId(activity.SpanId)
                .SetTraceId(activity.TraceId)
            : basicProperties;
    }

    private static BasicProperties SetHeader(this BasicProperties basicProperties, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);

        basicProperties = basicProperties.EnsureHeaders();

        basicProperties.Headers![key] = value;

        return basicProperties;
    }


    /// <summary>
    /// Set the TraceId in the BasicProperties
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="activityTraceId"></param>
    /// <returns></returns>
    private static BasicProperties SetTraceId(this BasicProperties basicProperties, ActivityTraceId? activityTraceId)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);

        return (activityTraceId != null)
            ? basicProperties.SetHeader("TraceId", activityTraceId.ToString())
            : basicProperties;
    }


    /// <summary>
    /// set the SpanId in the BasicProperties
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="activitySpanId"></param>
    /// <returns></returns>
    private static BasicProperties SetSpanId(this BasicProperties basicProperties, ActivitySpanId? activitySpanId)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        return (activitySpanId != null)
            ? basicProperties.SetHeader("SpanId", activitySpanId.ToString())
            : basicProperties;
    }

}
