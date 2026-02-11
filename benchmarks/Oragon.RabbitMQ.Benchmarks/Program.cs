using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Oragon.RabbitMQ.Benchmarks;
using Oragon.RabbitMQ.Benchmarks.Infrastructure;

static void PrintTimestamp(string label)
{
    DateTimeOffset now = DateTimeOffset.Now;
    Console.WriteLine($"[{label}] {now.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ} (UTC) / {now:yyyy-MM-ddTHH:mm:sszzz} (Local)");
}

// Smoke test mode: quick validation of all paths without BenchmarkDotNet
if (args.Contains("--smoke"))
{
    PrintTimestamp("SMOKE START");
    int failures = await SmokeTest.RunAsync().ConfigureAwait(false);
    PrintTimestamp("SMOKE END");
    return failures;
}

// Ensure RabbitMQ container is started before any benchmarks
PrintTimestamp("CONTAINER START");
Console.WriteLine("Starting RabbitMQ container...");
await RabbitMqFixture.WarmupAsync().ConfigureAwait(false);
Console.WriteLine("RabbitMQ container ready.");
PrintTimestamp("CONTAINER READY");

// Run benchmarks
PrintTimestamp("BENCHMARK START");

if (args.Length == 0)
{
    _ = BenchmarkSwitcher
        .FromAssembly(typeof(Program).Assembly)
        .Run(args, DefaultConfig.Instance
            .WithOptions(ConfigOptions.JoinSummary));
}
else
{
    _ = BenchmarkSwitcher
        .FromAssembly(typeof(Program).Assembly)
        .Run(args);
}

PrintTimestamp("BENCHMARK END");

return 0;
