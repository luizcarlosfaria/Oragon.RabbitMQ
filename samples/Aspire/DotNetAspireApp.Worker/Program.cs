using System.Text.Json;
using DotNetAspireApp.Worker;
using DotNetAspireApp.Worker.Areas;
using DotNetAspireApp.Worker.Extensions;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


string? serviceName = OpenTelemetryExtensions.GetOtelServiceName();
string? instanceId = OpenTelemetryExtensions.GetOtelInstanceId();


_ = builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Default);

builder.AddRabbitMQConsumer();

//_ = builder.Services.AddSingleton(sp => new ActivitySource("RabbitMQ.Gago", "1.0.0"));

builder.AddRabbitMQClient("rabbitmq", null, connectionFactory =>
    {
        connectionFactory.ClientProvidedName = $"DotNetAspireApp.Worker | {serviceName} | {instanceId}";
        connectionFactory.ConsumerDispatchConcurrency = DotNetAspireApp.Worker.Constants.ConsumerDispatchConcurrency;
        connectionFactory.TopologyRecoveryEnabled = false;
        connectionFactory.AutomaticRecoveryEnabled = false;
    }
);

_ = builder.Services.AddSingleton<EmailService>();

_ = builder.AddServiceDefaults();

// Add services to the container.
_ = builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
_ = app.UseExceptionHandler();

_ = app.MapGet("/", () => $"Alive at {DateTime.Now:yyyyy-MM-dd HH:mm}");

await app.Services.WaitRabbitMQAsync().ConfigureAwait(false);

await app.ConfigureRabbitMQAsync().ConfigureAwait(false);

await app.ConfigureManagedConsumer().ConfigureAwait(false); // Managed Implementation

await app.ConfigureUnManagedConsumer().ConfigureAwait(false); // UnManaged Implementation (Native Client Only)

_ = app.MapDefaultEndpoints();

app.Run();
