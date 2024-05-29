using RabbitMQ.Client;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Oragon.RabbitMQ;

public static partial class TelemetryExtensions
{

    public static Activity SafeStartActivity(this ActivitySource activitySource, [CallerMemberName] string name = "", ActivityKind kind = ActivityKind.Internal)
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        var activity = activitySource.StartActivity(name, kind) ?? new Activity("?" + name);
        activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    public static Activity SafeStartActivity(this ActivitySource activitySource, string name, ActivityKind kind, ActivityContext parentContext)
    {
        ArgumentNullException.ThrowIfNull(activitySource);
        var activity = activitySource.StartActivity(name, kind, parentContext) ?? new Activity("?" + name);
        activity.SetStartTime(DateTime.UtcNow);
        return activity;
    }

    public static ActivityTraceId GetTraceId(this BasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        return basicProperties.Headers != null && basicProperties.Headers.ContainsKey("TraceId")
            ? ActivityTraceId.CreateFromString(basicProperties.Headers.AsString("TraceId"))
            : default;
    }

    public static ActivitySpanId GetSpanId(this BasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (basicProperties.Headers != null && basicProperties.Headers.ContainsKey("SpanId"))
            return ActivitySpanId.CreateFromString(basicProperties.Headers.AsString("SpanId"));
        return default;
    }

    public static BasicProperties EnsureHeaders(this BasicProperties basicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        basicProperties.Headers ??= new Dictionary<string, object>();
        return basicProperties;
    }

    public static BasicProperties SetTelemetry(this BasicProperties basicProperties, Activity? activity)
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

    private static BasicProperties SetTraceId(this BasicProperties basicProperties, ActivityTraceId? activityTraceId)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (activityTraceId != null)
        {
            basicProperties.EnsureHeaders().Headers["TraceId"] = activityTraceId.ToString();
        }
        return basicProperties;
    }



    private static BasicProperties SetSpanId(this BasicProperties basicProperties, ActivitySpanId? activitySpanId)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (activitySpanId != null)
        {
            basicProperties.EnsureHeaders().Headers["SpanId"] = activitySpanId.ToString();
        }
        return basicProperties;
    }

}
