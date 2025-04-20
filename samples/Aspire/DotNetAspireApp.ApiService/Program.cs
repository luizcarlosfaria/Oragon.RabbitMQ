using System.Text.Json;
using DotNetAspireApp.ApiService;
using DotNetAspireApp.Common.Messages.Commands;
using Microsoft.AspNetCore.Mvc;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddTransient<MessagePublisher>();

builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Default);

builder.AddRabbitMQClient("rabbitmq", null, connectionFactory =>
{
    connectionFactory.ClientProvidedName = "DotNetAspireApp.ApiService";
    connectionFactory.AutomaticRecoveryEnabled = false;
    connectionFactory.TopologyRecoveryEnabled = false;
    connectionFactory.RequestedHeartbeat = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<RabbitMQ.AMQP.Client.IEnvironment>(serviceProvider =>
{
    var connectionFactory = serviceProvider.GetRequiredService<RabbitMQ.Client.IConnectionFactory>();
    return AmqpEnvironment.Create(ConnectionSettingsBuilder.Create()
        .Host(connectionFactory.Uri.Host)
        .VirtualHost(connectionFactory.VirtualHost)
        .User(connectionFactory.UserName)
        .Password(connectionFactory.Password)
        .Port(connectionFactory.Uri.Port)
        .Build());
});


// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    WeatherForecast[] forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

var progressBarWidth = 20;

app.MapPost("/enqueue", ([FromBody]DoSomethingRequest requestObject, CancellationToken cancellationToken, [FromServices] MessagePublisher messagePublisher)
    =>
{
    Action<string> Log = (string message) =>
    {
        Console.ForegroundColor = messagePublisher.consoleColors.ForegroundColor;
        Console.BackgroundColor = messagePublisher.consoleColors.BackgroundColor;
        Console.WriteLine($"{messagePublisher.ConsoleId} {message}");
        Console.ResetColor();
    };


    _ = Task.Run(async () =>
        {
            Log($"Starting to publish {requestObject.quantity:n0}");
            for (var i = 1; i <= requestObject.quantity; i++)
            {
                var command = new DoSomethingCommand(requestObject.Text, i, requestObject.quantity);

                await messagePublisher.PublishAsync(command, "events", string.Empty, default).ConfigureAwait(false);

                if (i % (requestObject.quantity / progressBarWidth) == 0) // Update progress bar
                {
                    var progress = (i * progressBarWidth / requestObject.quantity);
                    Log($"[{new string('#', progress)}{new string(' ', progressBarWidth - progress)}] {i * 100 / requestObject.quantity}%");
                }
                break;
            }
            Log($"Done ({requestObject.quantity:n0} messages!)");
            await messagePublisher.DisposeAsync().ConfigureAwait(false);
            Log($"END ({requestObject.quantity:n0} messages!)");
        });

    return Results.Ok();
});


app.MapDefaultEndpoints();

app.Run();

sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
