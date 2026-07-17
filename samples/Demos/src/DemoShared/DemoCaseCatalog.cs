namespace Oragon.RabbitMQ.Demos;

public static class DemoCaseCatalog
{
    public static IReadOnlyList<DemoCase> All { get; } =
    [
        new("01", "minimal-consumer", "Basic usage", "First functional consumer.", true,
        [
            "AddRabbitMQConsumer",
            "System.Text.Json serializer",
            "IConnection singleton",
            "MapQueue",
            "default Ack",
        ]),
        new("02", "standalone-topology-dlq", "Basic usage", "Standalone topology with DLQ.", true,
        [
            "WithTopology",
            "DLQ",
            "WhenSerializationFail",
            "WhenProcessFail",
            "WaitRabbitMQAsync",
        ]),
        new("03", "model-binding-lab", "Basic usage", "All relevant binders in one lab.", true,
        [
            "FromServices",
            "FromBody",
            "FromAmqpHeader",
            "BasicProperties",
            "deliveryCount/attempts",
        ]),
        new("04", "flow-control-results", "Basic usage", "AMQP results and ack semantics.", true,
        [
            "Ack",
            "Nack",
            "Reject",
            "Reply",
            "Forward",
            "WhenResultExecutionFail",
        ]),
        new("05", "rpc-request-reply", "Basic usage", "Request/reply with correlation.", true,
        [
            "ReplyTo",
            "CorrelationId",
            "Reply",
            "ReplyAndAck",
            "client timeout",
        ]),
        new("06", "concurrency-prefetch", "Basic usage", "Prefetch and dispatch concurrency tradeoffs.", true,
        [
            "WithPrefetch",
            "WithDispatchConcurrency",
            "ordering",
            "parallelism",
        ]),
        new("07", "serializers", "Basic usage", "System.Text.Json, Newtonsoft and keyed serializers.", true,
        [
            "AddSystemTextJsonAmqpSerializer",
            "AddNewtonsoftAmqpSerializer",
            "keyed IAmqpSerializer",
            "WithSerializer",
        ]),
        new("08", "aspire-worker", "Aspire", "Official Aspire worker sample.", true,
        [
            "AddRabbitMQClient",
            "health checks",
            "tracing",
            "AppHost",
            "ClientProvidedName",
        ]),
        new("09", "keyed-rabbitmq", "Aspire/keyed connections", "Multiple RabbitMQ connections.", true,
        [
            "AddKeyedRabbitMQClient",
            "keyed IConnection",
            "WithConnection",
        ]),
        new("10", "retry-quorum-delivery-count", "Reliability", "Retry with quorum queue delivery count.", true,
        [
            "quorum queue",
            "DLQ",
            "deliveryCount/attempts",
            "Reject(requeue:true)",
        ]),
        new("11", "graceful-shutdown", "Attention primitives", "Cooperative shutdown and in-flight drain.", true,
        [
            "WithGracefulShutdown",
            "context token cancellation",
            "in-flight drain",
            "DrainTimeout",
        ]),
        new("12", "requeue-to-tail", "Attention primitives", "Fairness by republishing to the queue tail.", true,
        [
            "RequeueToTail",
            "publish before ack",
            "property preservation",
            "broker header filtering",
        ]),
        new("13", "dynamic-queue-consumer", "Attention primitives", "Runtime-selected queue consumption windows.", true,
        [
            "IAmqpDynamicQueueConsumer",
            "MaxMessages",
            "MaxDuration",
            "IdleTimeout",
            "StopAfterInitialQueueLength",
        ]),
        new("14", "attention-with-primitives", "Attention primitives", "Full attention pattern composed from generic primitives.", true,
        [
            "MapQueue",
            "IAmqpDynamicQueueConsumer",
            "application-owned gate",
            "RequeueToTail",
            "no MapAttentionQueue",
        ]),
        new("15", "observability-dashboard", "Operations", "Operational signals and RabbitMQ Management.", true,
        [
            "structured logs",
            "health checks",
            "tracing",
            "DLQ inspection",
        ]),
        new("16", "application-gates", "Extension points", "Application-owned gates and leases.", true,
        [
            "IAmqpConcurrencyGate",
            "IServiceProvider hooks",
            "application-defined lock key",
            "optional Redis in demo only",
        ]),
        new("17", "publish-consistency-work-attention", "Consistency", "Publish work and attention with explicit confirms.", true,
        [
            "publisher confirmations",
            "mandatory publish",
            "publish work then attention",
            "application outbox guidance",
        ]),
    ];

    public static DemoCase? Find(string commandOrId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandOrId);

        return All.FirstOrDefault(demo =>
            string.Equals(demo.Id, commandOrId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(demo.Command, commandOrId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(demo.Slug, commandOrId, StringComparison.OrdinalIgnoreCase));
    }
}
