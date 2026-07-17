using Oragon.RabbitMQ.Demos;

string command = args.Length == 0 ? "help" : args[0];

return await (command.ToLowerInvariant() switch
{
    "help" or "--help" or "-h" => Task.FromResult(PrintHelp()),
    "list" => Task.FromResult(PrintList()),
    "describe" => Task.FromResult(Describe(args.Skip(1).FirstOrDefault())),
    _ => RunCaseAsync(command),
}).ConfigureAwait(false);

static int PrintHelp()
{
    Console.WriteLine("Oragon.RabbitMQ demos");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  list                         Lists all demo cases.");
    Console.WriteLine("  describe <id|slug|command>   Shows one demo case.");
    Console.WriteLine("  <case-command>               Runs an implemented demo case.");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project samples/Demos/src/DemoHost -- 01-minimal-consumer");
    Console.WriteLine("  dotnet run --project samples/Demos/src/DemoHost -- list");
    Console.WriteLine("  dotnet run --project samples/Demos/src/DemoHost -- describe 13-dynamic-queue-consumer");
    return 0;
}

static int PrintList()
{
    foreach (DemoCase demo in DemoCaseCatalog.All)
    {
        string broker = demo.RequiresRabbitMq ? "broker" : "no-broker";
        Console.WriteLine($"{demo.Command,-43} {demo.Phase,-24} {broker}");
    }

    return 0;
}

static int Describe(string? commandOrId)
{
    if (string.IsNullOrWhiteSpace(commandOrId))
    {
        Console.Error.WriteLine("Missing case id, slug or command.");
        return 2;
    }

    DemoCase? demo = DemoCaseCatalog.Find(commandOrId);
    if (demo == null)
    {
        Console.Error.WriteLine($"Unknown demo case: {commandOrId}");
        return 2;
    }

    Console.WriteLine($"{demo.Command}");
    Console.WriteLine();
    Console.WriteLine($"Phase: {demo.Phase}");
    Console.WriteLine($"Requires RabbitMQ: {demo.RequiresRabbitMq}");
    Console.WriteLine($"README: {demo.ReadmePath}");
    Console.WriteLine();
    Console.WriteLine(demo.Objective);
    Console.WriteLine();
    Console.WriteLine("Features:");
    foreach (string feature in demo.Features)
    {
        Console.WriteLine($"- {feature}");
    }

    return 0;
}

static async Task<int> RunCaseAsync(string commandOrId)
{
    DemoCase? demo = DemoCaseCatalog.Find(commandOrId);
    if (demo == null)
    {
        Console.Error.WriteLine($"Unknown command or demo case: {commandOrId}");
        Console.Error.WriteLine("Run `list` to see available cases.");
        return 2;
    }

    if (string.Equals(demo.Command, "01-minimal-consumer", StringComparison.OrdinalIgnoreCase))
    {
        return await MinimalConsumerDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "02-standalone-topology-dlq", StringComparison.OrdinalIgnoreCase))
    {
        return await StandaloneTopologyDlqDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "03-model-binding-lab", StringComparison.OrdinalIgnoreCase))
    {
        return await ModelBindingLabDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "04-flow-control-results", StringComparison.OrdinalIgnoreCase))
    {
        return await FlowControlResultsDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "05-rpc-request-reply", StringComparison.OrdinalIgnoreCase))
    {
        return await RpcRequestReplyDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "06-concurrency-prefetch", StringComparison.OrdinalIgnoreCase))
    {
        return await ConcurrencyPrefetchDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "07-serializers", StringComparison.OrdinalIgnoreCase))
    {
        return await SerializersDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "08-aspire-worker", StringComparison.OrdinalIgnoreCase))
    {
        return await AspireWorkerDemo.RunAsync(demo).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "09-keyed-rabbitmq", StringComparison.OrdinalIgnoreCase))
    {
        return await KeyedRabbitMqDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "10-retry-quorum-delivery-count", StringComparison.OrdinalIgnoreCase))
    {
        return await RetryQuorumDeliveryCountDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "11-graceful-shutdown", StringComparison.OrdinalIgnoreCase))
    {
        return await GracefulShutdownDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "12-requeue-to-tail", StringComparison.OrdinalIgnoreCase))
    {
        return await RequeueToTailDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "13-dynamic-queue-consumer", StringComparison.OrdinalIgnoreCase))
    {
        return await DynamicQueueConsumerDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "14-attention-with-primitives", StringComparison.OrdinalIgnoreCase))
    {
        return await AttentionWithPrimitivesDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "15-observability-dashboard", StringComparison.OrdinalIgnoreCase))
    {
        return await ObservabilityDashboardDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "16-application-gates", StringComparison.OrdinalIgnoreCase))
    {
        return await ApplicationGatesDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    if (string.Equals(demo.Command, "17-publish-consistency-work-attention", StringComparison.OrdinalIgnoreCase))
    {
        return await PublishConsistencyWorkAttentionDemo.RunAsync(demo, new DemoOptions()).ConfigureAwait(false);
    }

    Console.WriteLine($"{demo.Command} runner is pending.");
    Console.WriteLine($"See {demo.ReadmePath} and spec/demo-cases-roadmap.md.");
    return 1;
}
