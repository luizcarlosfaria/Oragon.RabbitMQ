# Attention primitives sample

This sample shows the intended composition model for the attention milestone.
It does not introduce `MapAttentionQueue(...)` and does not depend on Redis.
If the application needs Redis, SQL, etc., it resolves its own provider from
`IServiceProvider` inside hooks or gates.

```csharp
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.DynamicQueues;

builder.Services.AddRabbitMQConsumer();

app.MapQueue(
        "attention.channel",
        async (
            AttentionRequest attention,
            IAmqpDynamicQueueConsumer dynamicConsumer,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var gate = services.GetService<IAmqpConcurrencyGate>();
            IAmqpConcurrencyLease lease = null;

            if (gate != null)
            {
                lease = await gate.TryAcquireAsync(
                    new AmqpConcurrencyGateRequest(
                        Key: $"attention:{attention.Type}:{attention.ChannelId}",
                        LeaseTime: TimeSpan.FromMinutes(2),
                        Metadata: new Dictionary<string, object>
                        {
                            ["channelId"] = attention.ChannelId,
                            ["type"] = attention.Type,
                        }),
                    cancellationToken);

                if (!lease.Acquired)
                {
                    return AmqpResults.Ack();
                }
            }

            await using (lease)
            {
                DynamicQueueConsumeResult result = await dynamicConsumer.ConsumeAsync(
                    new DynamicQueueConsumeRequest<WorkMessage>
                    {
                        QueueName = attention.WorkQueueName,
                        MaxMessages = attention.MaxMessages,
                        MaxDuration = attention.MaxDuration,
                        IdleTimeout = attention.IdleTimeout,
                        StopAfterInitialQueueLength = attention.StopAfterInitialQueueLength,
                        PrefetchCount = 10,
                        MaxLocalConcurrency = 4,
                        Metadata = new Dictionary<string, object>
                        {
                            ["attentionId"] = attention.AttentionId,
                            ["channelId"] = attention.ChannelId,
                        },
                        BeforeStartAsync = (context, ct) =>
                        {
                            // Resolve application services here when needed.
                            // The library does not own Redis, lock keys, or domain lifecycle.
                            return ValueTask.FromResult(DynamicQueueStartDecision.Allow());
                        },
                        AfterStopAsync = (context, ct) =>
                        {
                            // Persist metrics/result if the application wants to.
                            return ValueTask.CompletedTask;
                        },
                        OnMessageAsync = async (message, context) =>
                        {
                            var processor = context.Services.GetRequiredService<WorkProcessor>();
                            await processor.ProcessAsync(message, context.CancellationToken);
                            return DynamicQueueMessageResult.Ack();
                        },
                    },
                    cancellationToken);

                return result.Status == DynamicQueueConsumeStatus.Faulted
                    ? AmqpResults.Nack(false)
                    : AmqpResults.Ack();
            }
        })
    .WithGracefulShutdown(options =>
    {
        options.CancelContextTokenOnStop = true;
        options.WaitForInFlightMessages = true;
        options.DrainTimeout = TimeSpan.FromSeconds(30);
    })
    .WithTopology(async (services, channel, cancellationToken) =>
    {
        await channel.QueueDeclareAsync(
            queue: "attention.channel",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: QueueArguments
                .Quorum()
                .WithDeadLetter("attention.dlx", "attention.failed"),
            cancellationToken: cancellationToken);
    });
```

The stop rules are independent. A request may use only one of them or combine
several; the dynamic consumer stops at the first reached rule:

- `MaxMessages`
- `MaxDuration`
- `IdleTimeout`
- `StopAfterInitialQueueLength`
- external cancellation token

`IAmqpConcurrencyGate` is intentionally generic. The application chooses the key
format, storage technology, failure policy, and lease semantics.
