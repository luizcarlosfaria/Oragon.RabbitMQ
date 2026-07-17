namespace Oragon.RabbitMQ.Demos;

internal static class AspireWorkerDemo
{
    public static async Task<int> RunAsync(DemoCase demo)
    {
        ArgumentNullException.ThrowIfNull(demo);

        string root = FindRepositoryRoot();
        string appHostProgram = Path.Combine(root, "samples", "Aspire", "DotNetAspireApp.AppHost", "Program.cs");
        string workerProgram = Path.Combine(root, "samples", "Aspire", "DotNetAspireApp.Worker", "Program.cs");
        string workerManagedConsumer = Path.Combine(root, "samples", "Aspire", "DotNetAspireApp.Worker", "Extensions", "ManagedConsumerExtensions.cs");
        string apiProgram = Path.Combine(root, "samples", "Aspire", "DotNetAspireApp.ApiService", "Program.cs");
        string webProgram = Path.Combine(root, "samples", "Aspire", "DotNetAspireApp.Web", "Program.cs");
        string serviceDefaults = Path.Combine(root, "samples", "Aspire", "DotNetAspireApp.ServiceDefaults", "Extensions.cs");

        Console.WriteLine("Aspire sample path: samples/Aspire");
        Console.WriteLine("AppHost command:");
        Console.WriteLine("dotnet run --project samples/Aspire/DotNetAspireApp.AppHost");

        var failures = new List<string>();

        await CheckFileContainsAsync(failures, appHostProgram, "AddRabbitMQ(\"rabbitmq").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, appHostProgram, "DotNetAspireApp_ApiService").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, appHostProgram, "DotNetAspireApp_Worker").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, appHostProgram, "DotNetAspireApp_Web").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, appHostProgram, "WithReference(rabbitmq)").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, workerProgram, "AddRabbitMQConsumer").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, workerProgram, "AddRabbitMQClient(\"rabbitmq").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, workerProgram, "WaitRabbitMQAsync").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, workerProgram, "ConfigureRabbitMQAsync").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, workerManagedConsumer, "MapQueue").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, apiProgram, "AddRabbitMQClient(\"rabbitmq").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, webProgram, "AddServiceDefaults").ConfigureAwait(false);
        await CheckFileContainsAsync(failures, serviceDefaults, "AddRabbitMQInstrumentation").ConfigureAwait(false);

        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        Console.WriteLine("AppHost declares RabbitMQ, API, Worker and Web resources.");
        Console.WriteLine("Worker uses AddRabbitMQClient, AddRabbitMQConsumer and MapQueue.");
        Console.WriteLine("ServiceDefaults includes RabbitMQ OpenTelemetry instrumentation.");
        Console.WriteLine(failures.Count == 0 ? "Demo 08 source verification succeeded." : "Demo 08 source verification failed.");

        return failures.Count == 0 ? 0 : 1;
    }

    private static async Task CheckFileContainsAsync(
        List<string> failures,
        string path,
        string expected)
    {
        if (!File.Exists(path))
        {
            failures.Add($"Missing file: {Path.GetRelativePath(FindRepositoryRoot(), path)}");
            return;
        }

        string content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        if (!content.Contains(expected, StringComparison.Ordinal))
        {
            failures.Add($"Missing `{expected}` in {Path.GetRelativePath(FindRepositoryRoot(), path)}");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Environment.CurrentDirectory);

        while (directory != null)
        {
            string solutionPath = Path.Combine(directory.FullName, "Oragon.RabbitMQ.slnx");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Oragon.RabbitMQ.slnx from the current directory.");
    }
}
