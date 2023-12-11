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
        if (activitySource is null) throw new ArgumentNullException(nameof(activitySource));
        Activity activity = activitySource.StartActivity(name, kind) ?? new Activity(name);
        activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    public static Activity SafeStartActivity(this ActivitySource activitySource, string name, ActivityKind kind, ActivityContext parentContext)
    {
        if (activitySource is null) throw new ArgumentNullException(nameof(activitySource));
        Activity activity = activitySource.StartActivity(name, kind, parentContext) ?? new Activity(name);
        activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    public static ActivityTraceId GetTraceId(this IBasicProperties basicProperties)
    {
        if (basicProperties is null) throw new ArgumentNullException(nameof(basicProperties));
        return basicProperties.Headers != null && basicProperties.Headers.ContainsKey("TraceId")
            ? ActivityTraceId.CreateFromString(basicProperties.Headers.AsString("TraceId"))
            : default;
    }

    public static ActivitySpanId GetSpanId(this IBasicProperties basicProperties)
    {
        if (basicProperties is null) throw new ArgumentNullException(nameof(basicProperties));
        if (basicProperties.Headers != null && basicProperties.Headers.ContainsKey("SpanId"))
            return ActivitySpanId.CreateFromString(basicProperties.Headers.AsString("SpanId"));
        return default;
    }

    public static IBasicProperties SetTelemetry(this IBasicProperties basicProperties, Activity? activity)
    {        
        if (basicProperties is null) throw new ArgumentNullException(nameof(basicProperties));
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
        if (basicProperties is null) throw new ArgumentNullException(nameof(basicProperties));
        if (activityTraceId != null)
        {
            if (basicProperties.Headers == null) basicProperties.Headers = new Dictionary<string, object>();
            basicProperties.Headers["TraceId"] = activityTraceId.ToString();
        }
        return basicProperties;
    }

    private static IBasicProperties SetSpanId(this IBasicProperties basicProperties, ActivitySpanId? activitySpanId)
    {
        if (basicProperties is null) throw new ArgumentNullException(nameof(basicProperties));
        if (activitySpanId != null)
        {
            if (basicProperties.Headers == null) basicProperties.Headers = new Dictionary<string, object>();
            basicProperties.Headers["SpanId"] = activitySpanId.ToString();
        }
        return basicProperties;
    }

}
