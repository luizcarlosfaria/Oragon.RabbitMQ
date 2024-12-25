using System.Text.Json;
using DotNetAspireApp.ApiService;
using DotNetAspireApp.Common.Messages.Commands;
using Microsoft.AspNetCore.Mvc;
using Oragon.RabbitMQ.AspireClient;
using Oragon.RabbitMQ.Serialization;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddTransient<MessagePublisher>();

builder.Services.AddSingleton<IAMQPSerializer>(sp => new SystemTextJsonAMQPSerializer(JsonSerializerOptions.Default));

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

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

int progressBarWidth = 20;

app.MapPost("/enqueue", (DoSomethingRequest req, CancellationToken cancellationToken, [FromServices] MessagePublisher messagePublisher)
    =>
{
    _ = Task.Run(async () =>
    {
        Console.WriteLine($"{messagePublisher.Id} | Starting to publish {req.quantity:n0}");
        for (int i = 1; i <= req.quantity; i++)
        {
            var command = new DoSomethingCommand(req.Text, i, req.quantity);

            await messagePublisher.PublishAsync(command, "events", string.Empty, default).ConfigureAwait(false);

            if (i % (req.quantity / progressBarWidth) == 0) // Update progress bar
            {
                int progress = (i * progressBarWidth / req.quantity);
                Console.WriteLine($"{messagePublisher.Id} | [{new string('#', progress)}{new string(' ', progressBarWidth - progress)}] {i * 100 / req.quantity}%");
            }

        }
        Console.WriteLine($"{messagePublisher.Id} | Done ({req.quantity:n0} messages!)");
        await messagePublisher.DisposeAsync().ConfigureAwait(false);
        Console.WriteLine($"{messagePublisher.Id} | END ({req.quantity:n0} messages!)");
    });

    return "ok";
});


app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
