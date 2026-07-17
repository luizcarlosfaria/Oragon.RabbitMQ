using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.Demos;

internal static class ModelBindingLabDemo
{
    public static async Task<int> RunAsync(DemoCase demo, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(options);

        string queueName = options.ResourceName(demo, "input");
        string exchangeName = options.ResourceName(demo, "exchange");
        const string routingKey = "bindings";
        const string consumerTag = "oragon-demo-03-model-binding-lab";
        string userId = GetUserId(options);

        var message = new ModelBindingLabMessage("binding-message", 42);
        var observed = new TaskCompletionSource<ModelBindingObservation>(TaskCreationOptions.RunContinuationsAsynchronously);

        Console.WriteLine($"AMQP URI: {options.AmqpUri}");
        Console.WriteLine($"Exchange: {exchangeName}");
        Console.WriteLine($"Queue: {queueName}");

        using IConnection connection = await RabbitMqDemoClient.CreateConnectionAsync(options).ConfigureAwait(false);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));
        builder.Services.AddRabbitMQConsumer();
        builder.Services.AddSystemTextJsonAmqpSerializer();
        builder.Services.AddSingleton(connection);
        builder.Services.AddSingleton(new ModelBindingProbeService("default-service"));
        builder.Services.AddKeyedSingleton("keyed-service", new ModelBindingProbeService("keyed-service"));

        IHost host = builder.Build();
        bool hostStarted = false;
        bool hostStopped = false;
        try
        {
            _ = host.Services
                .MapQueue(queueName, (
                    [FromBody] ModelBindingLabMessage body,
                    [FromServices] ModelBindingProbeService service,
                    [FromServices("keyed-service")] ModelBindingProbeService keyedService,
                    [FromAmqpHeader("x-string")] string headerText,
                    [FromAmqpHeader("x-number")] int headerNumber,
                    [FromAmqpHeader("x-enabled")] bool headerEnabled,
                    IConnection boundConnection,
                    IChannel channel,
                    BasicDeliverEventArgs request,
                    IReadOnlyBasicProperties basicProperties,
                    IServiceProvider services,
                    IAmqpContext context,
                    CancellationToken cancellationToken,
                    DeliveryModes deliveryMode,
                    IDictionary<string, object> headers,
                    AmqpTimestamp timestamp,
                    string queueName,
                    string exchangeName,
                    string routingKey,
                    string consumerTag,
                    byte priority,
                    long deliveryCount,
                    int? attempts,
                    string contentType,
                    string contentEncoding,
                    string correlationId,
                    string replyTo,
                    string expiration,
                    string messageId,
                    string type,
                    string userId,
                    string appId,
                    string clusterId) =>
                {
                    var failures = new List<string>();
                    Check(failures, "body", body == message, $"{body}");
                    Check(failures, "service", service.Name == "default-service", service.Name);
                    Check(failures, "keyedService", keyedService.Name == "keyed-service", keyedService.Name);
                    Check(failures, "headerText", headerText == "header-value", headerText);
                    Check(failures, "headerNumber", headerNumber == 123, headerNumber);
                    Check(failures, "headerEnabled", headerEnabled, headerEnabled);
                    Check(failures, "connection", ReferenceEquals(boundConnection, connection), "not singleton connection");
                    Check(failures, "channel", channel.IsOpen, "channel closed");
                    Check(failures, "request", request.DeliveryTag > 0, request.DeliveryTag);
                    Check(failures, "basicProperties", basicProperties.MessageId == "binding-message-id", basicProperties.MessageId);
                    Check(failures, "services", ReferenceEquals(services, context.ServiceProvider), "not context service provider");
                    Check(failures, "context", context.QueueName == queueName, context.QueueName);
                    Check(failures, "cancellationToken", cancellationToken.CanBeCanceled, cancellationToken.CanBeCanceled);
                    Check(failures, "deliveryMode", deliveryMode == DeliveryModes.Persistent, deliveryMode);
                    Check(failures, "headers", headers.ContainsKey("x-string"), string.Join(",", headers.Keys));
                    Check(failures, "timestamp", timestamp.UnixTime == 1_700_000_000L, timestamp.UnixTime);
                    Check(failures, "queueName", queueName == context.QueueName, queueName);
                    Check(failures, "exchangeName", exchangeName == request.Exchange, exchangeName);
                    Check(failures, "routingKey", routingKey == request.RoutingKey, routingKey);
                    Check(failures, "consumerTag", consumerTag == request.ConsumerTag, consumerTag);
                    Check(failures, "priority", priority == 7, priority);
                    Check(failures, "deliveryCount", deliveryCount == 3L, deliveryCount);
                    Check(failures, "attempts", attempts == 3, attempts);
                    Check(failures, "contentType", contentType == "application/json", contentType);
                    Check(failures, "contentEncoding", contentEncoding == "utf-8", contentEncoding);
                    Check(failures, "correlationId", correlationId == "binding-correlation-id", correlationId);
                    Check(failures, "replyTo", replyTo == "binding-reply-to", replyTo);
                    Check(failures, "expiration", expiration == "60000", expiration);
                    Check(failures, "messageId", messageId == "binding-message-id", messageId);
                    Check(failures, "type", type == "model-binding-lab", type);
                    Check(failures, "userId", userId == GetUserId(options), userId);
                    Check(failures, "appId", appId == "oragon-rabbitmq-demos", appId);
                    Check(failures, "clusterId", clusterId == "oragon-demo-cluster", clusterId);

                    _ = observed.TrySetResult(new ModelBindingObservation(failures));
                    return Task.CompletedTask;
                })
                .WithPrefetch(1)
                .WithConsumerTag(consumerTag)
                .WithConnection((services, cancellationToken) =>
                    Task.FromResult(services.GetRequiredService<IConnection>()))
                .WithSerializer(services =>
                    services.GetRequiredService<IAmqpSerializer>())
                .WithTopology(async (services, channel, cancellationToken) =>
                {
                    await channel.ExchangeDeclareAsync(
                        exchange: exchangeName,
                        type: ExchangeType.Direct,
                        durable: true,
                        autoDelete: false,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueueDeclareAsync(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    await channel.QueueBindAsync(
                        queue: queueName,
                        exchange: exchangeName,
                        routingKey: routingKey,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await channel.QueuePurgeAsync(queueName, cancellationToken).ConfigureAwait(false);
                });

            await host.StartAsync().ConfigureAwait(false);
            hostStarted = true;

            using IChannel publishChannel = await RabbitMqDemoClient.CreatePublishChannelAsync(connection).ConfigureAwait(false);
            await PublishBindingMessageAsync(
                publishChannel,
                exchangeName,
                routingKey,
                message,
                userId).ConfigureAwait(false);

            ModelBindingObservation result = await observed.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            await host.StopAsync().ConfigureAwait(false);
            hostStopped = true;

            QueueDeclareOk state = await publishChannel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);
            bool success = result.Failures.Count == 0 && state.MessageCount == 0;

            foreach (string failure in result.Failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.WriteLine($"Binding failures: {result.Failures.Count}");
            Console.WriteLine($"Ready messages after processing: {state.MessageCount}");
            Console.WriteLine(success ? "Demo 03 succeeded." : "Demo 03 failed.");

            return success ? 0 : 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"Timed out waiting for model binding observation: {exception.Message}");
            return 1;
        }
        finally
        {
            if (hostStarted && !hostStopped)
            {
                await host.StopAsync().ConfigureAwait(false);
            }

            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                host.Dispose();
            }
        }
    }

    private static async Task PublishBindingMessageAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        ModelBindingLabMessage message,
        string userId)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            Headers = new Dictionary<string, object?>
            {
                ["x-string"] = "header-value",
                ["x-number"] = 123,
                ["x-enabled"] = true,
                [AmqpHeaders.XDeliveryCountHeader] = 3L,
            },
            DeliveryMode = DeliveryModes.Persistent,
            Priority = 7,
            CorrelationId = "binding-correlation-id",
            ReplyTo = "binding-reply-to",
            Expiration = "60000",
            MessageId = "binding-message-id",
            Timestamp = new AmqpTimestamp(1_700_000_000L),
            Type = "model-binding-lab",
            UserId = userId,
            AppId = "oragon-rabbitmq-demos",
            ClusterId = "oragon-demo-cluster",
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(message)).ConfigureAwait(false);
    }

    private static string GetUserId(DemoOptions options)
    {
        Uri uri = new(options.AmqpUri);
        string userInfo = uri.UserInfo;
        if (string.IsNullOrWhiteSpace(userInfo))
        {
            return "guest";
        }

        int separatorIndex = userInfo.IndexOf(':', StringComparison.Ordinal);
        string user = separatorIndex >= 0 ? userInfo[..separatorIndex] : userInfo;
        return Uri.UnescapeDataString(user);
    }

    private static void Check(List<string> failures, string name, bool success, object? actual)
    {
        if (!success)
        {
            string actualText = Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "(null)";
            failures.Add($"{name} binding mismatch. Actual: {actualText}");
        }
    }
}

internal sealed record ModelBindingLabMessage(string Id, int Value);

internal sealed record ModelBindingProbeService(string Name);

internal sealed record ModelBindingObservation(IReadOnlyList<string> Failures);
