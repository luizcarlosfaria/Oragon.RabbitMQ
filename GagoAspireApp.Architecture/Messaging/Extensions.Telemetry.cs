using GagoAspireApp.Architecture.Messaging;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace GagoAspireApp.Architecture.Messaging;

public static partial class TelemetryExtensions
{

    public static Activity SafeStartActivity(this ActivitySource activitySource, [CallerMemberName] string name = "", ActivityKind kind = ActivityKind.Internal)
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        Activity activity = activitySource.StartActivity(name, kind) ?? new Activity("?" + name);
        activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    public static Activity SafeStartActivity(this ActivitySource activitySource, string name, ActivityKind kind, ActivityContext parentContext)
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        Activity activity = activitySource.StartActivity(name, kind, parentContext) ?? new Activity("?" + name);
        activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    public static ActivityTraceId GetTraceId(this IBasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        return basicProperties.Headers != null && basicProperties.Headers.ContainsKey("TraceId")
            ? ActivityTraceId.CreateFromString(basicProperties.Headers.AsString("TraceId"))
            : default;
    }

    public static ActivitySpanId GetSpanId(this IBasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (basicProperties.Headers != null && basicProperties.Headers.ContainsKey("SpanId"))
            return ActivitySpanId.CreateFromString(basicProperties.Headers.AsString("SpanId"));
        return default;
    }

    public static IBasicProperties EnsureHeaders(this IBasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        basicProperties.Headers ??= new Dictionary<string, object>();
        return basicProperties;
    }

    public static IBasicProperties SetTelemetry(this IBasicProperties basicProperties, Activity? activity)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (activity != null)
        {
            basicProperties
                .SetSpanId(activity.SpanId)
                .SetTraceId(activity.TraceId);
        }
        return basicProperties;
    }

    private static IBasicProperties SetTraceId(this IBasicProperties basicProperties, ActivityTraceId? activityTraceId)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (activityTraceId != null)
        {
            basicProperties.EnsureHeaders().Headers["TraceId"] = activityTraceId.ToString();
        }
        return basicProperties;
    }

    

    private static IBasicProperties SetSpanId(this IBasicProperties basicProperties, ActivitySpanId? activitySpanId)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (activitySpanId != null)
        {
            basicProperties.EnsureHeaders().Headers["SpanId"] = activitySpanId.ToString();
        }
        return basicProperties;
    }

}
