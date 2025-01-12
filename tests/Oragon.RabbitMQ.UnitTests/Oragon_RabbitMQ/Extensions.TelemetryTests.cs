using System.Diagnostics;
using Dawn;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class Extensions_Telemetry_Tests
{
    [Fact]
    public void SafeStartActivity_ShouldStartActivityWithGivenNameAndKind()
    {
        // Arrange
        var activitySource = new ActivitySource("TestActivitySource");
        var name = "TestActivity";
        var kind = ActivityKind.Internal;

        // Act
        var activity = activitySource.SafeStartActivity(kind, name);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(name, activity.OperationName);
        Assert.Equal(kind, activity.Kind);
    }

    [Fact]
    public void SafeStartActivity_ShouldStartActivityWithGivenNameKindAndParentContext()
    {
        // Arrange
        var activitySource = new ActivitySource("TestActivitySource");
        var name = "TestActivity";
        var kind = ActivityKind.Internal;
        var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

        // Act
        var activity = activitySource.SafeStartActivity(kind, parentContext, name);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(name, activity.OperationName);
        Assert.Equal(kind, activity.Kind);
        //Assert.Equal(parentContext, activity.Parent);
    }

    [Fact]
    public void GetTraceId_ShouldReturnTraceIdFromBasicProperties()
    {
        // Arrange
        var activityTraceId = ActivityTraceId.CreateRandom();
        var basicProperties = new BasicProperties
        {
            Headers = new Dictionary<string, object>
            {
                ["TraceId"] = activityTraceId.ToString()
            }
        };

        // Act
        var traceId = basicProperties.GetTraceId();

        // Assert
        Assert.Equal(activityTraceId.ToString(), traceId.ToString());
    }

    [Fact]
    public void GetTraceId_ShouldReturnDefaultTraceIdWhenHeadersDoNotExist()
    {
        // Arrange
        var basicProperties = new BasicProperties();

        // Act
        var traceId = basicProperties.GetTraceId();

        // Assert
        Assert.Equal(default, traceId);
    }

    [Fact]
    public void GetSpanId_ShouldReturnSpanIdFromBasicProperties()
    {
        // Arrange
        var activitySpanId = ActivitySpanId.CreateRandom();
        var basicProperties = new BasicProperties
        {
            Headers = new Dictionary<string, object>
            {
                ["SpanId"] = activitySpanId.ToString()
            }
        };

        // Act
        var spanId = basicProperties.GetSpanId();

        // Assert
        Assert.Equal(activitySpanId.ToString(), spanId.ToString());
    }

    [Fact]
    public void GetSpanId_ShouldReturnDefaultSpanIdWhenHeadersDoNotExist()
    {
        // Arrange
        var basicProperties = new BasicProperties();

        // Act
        var spanId = basicProperties.GetSpanId();

        // Assert
        Assert.Equal(default, spanId);
    }

    [Fact]
    public void EnsureHeaders_ShouldCreateNewDictionaryWhenHeadersDoNotExist()
    {
        // Arrange
        var basicProperties = new BasicProperties();

        // Act
        var result = basicProperties.EnsureHeaders();

        // Assert
        Assert.NotNull(result.Headers);
        _ = Assert.IsType<Dictionary<string, object>>(result.Headers);
    }

    [Fact]
    public void EnsureHeaders_ShouldNotCreateNewDictionaryWhenHeadersExist()
    {
        // Arrange
        var headers = new Dictionary<string, object>();
        var basicProperties = new BasicProperties
        {
            Headers = headers
        };

        // Act
        var result = basicProperties.EnsureHeaders();

        // Assert
        Assert.Same(headers, result.Headers);
    }

    [Fact]
    public void SetTelemetry_ShouldSetTraceIdAndSpanIdInBasicProperties()
    {
        // Arrange
        var basicProperties = new BasicProperties();
        var activitySource = new ActivitySource("TestActivitySource");



        var activityTraceId = ActivityTraceId.CreateRandom();
        var activitySpanId = ActivitySpanId.CreateRandom();

        using var activity = new Activity("TestActivity")
            .SetIdFormat(ActivityIdFormat.W3C)
            .SetParentId(activityTraceId, activitySpanId)
            .Start();


        // Act
        var result = basicProperties.SetTelemetry(activity);

        // Assert
        _ = Guard.Argument(result.Headers).NotNull();
        Assert.Equal(activity.TraceId.ToString(), result.Headers["TraceId"]);
        Assert.Equal(activity.SpanId.ToString(), result.Headers["SpanId"]);
    }

    [Fact]
    public void SetTelemetry_ShouldNotSetTraceIdAndSpanIdInBasicPropertiesWhenActivityIsNull()
    {
        // Arrange
        var basicProperties = new BasicProperties();

        // Act
        var result = basicProperties.SetTelemetry(null);

        // Assert
        Assert.Null(result.Headers);
    }
}
