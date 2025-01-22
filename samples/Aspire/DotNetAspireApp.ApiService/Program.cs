using System.Text.Json;
using DotNetAspireApp.ApiService;
using DotNetAspireApp.Common.Messages.Commands;
using Microsoft.AspNetCore.Mvc;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddTransient<MessagePublisher>();

builder.Services.AddAmqpSerializer(options: JsonSerializerOptions.Default);

builder.AddRabbitMQClient("rabbitmq", null, connectionFactory =>
{
    connectionFactory.ClientProvidedName = "DotNetAspireApp.ApiService";
    connectionFactory.AutomaticRecoveryEnabled = false;
    connectionFactory.TopologyRecoveryEnabled = false;
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

app.MapPost("/enqueue", (DoSomethingRequest req, CancellationToken cancellationToken, [FromServices] MessagePublisher messagePublisher)
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
            Log($"Starting to publish {req.quantity:n0}");
            for (var i = 1; i <= req.quantity; i++)
            {
                var command = new DoSomethingCommand(req.Text, i, req.quantity);

                await messagePublisher.PublishAsync(command, "events", string.Empty, default).ConfigureAwait(false);

                if (i % (req.quantity / progressBarWidth) == 0) // Update progress bar
                {
                    var progress = (i * progressBarWidth / req.quantity);
                    Log($"[{new string('#', progress)}{new string(' ', progressBarWidth - progress)}] {i * 100 / req.quantity}%");
                }

            }
            Log($"Done ({req.quantity:n0} messages!)");
            await messagePublisher.DisposeAsync().ConfigureAwait(false);
            Log($"END ({req.quantity:n0} messages!)");
        });

    return Results.Ok();
});


app.MapDefaultEndpoints();

app.Run();

sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
