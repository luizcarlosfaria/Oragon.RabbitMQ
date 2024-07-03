using System.Text.Json;
using DotNetAspireApp.Common.Messages.Commands;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Publisher;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<MessagePublisher>();
builder.Services.AddSingleton<IAMQPSerializer>(sp => new SystemTextJsonAMQPSerializer(JsonSerializerOptions.Default));

builder.AddRabbitMQClient("rabbitmq", null, connectionFactory =>
{
    connectionFactory.ClientProvidedName = "DotNetAspireApp.ApiService";
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

app.MapPost("/enqueue", async (DoSomethingRequest req, [FromServices] MessagePublisher messagePublisher, [FromServices] IConnectionFactory connectionFactory)
    =>
{
    _ = Task.Run(async () => {

        using var connection = await connectionFactory.CreateConnectionAsync(CancellationToken.None).ConfigureAwait(true);

        using var channel = await connection.CreateChannelAsync(CancellationToken.None).ConfigureAwait(true);

        await Parallel.ForAsync(1, req.quantity + 1, async (currentSeq, ct) =>
        {
            var command = new DoSomethingCommand(req.Text, currentSeq, req.quantity);

            await messagePublisher
                .SendAsync(channel, "", "events", command, ct)
                .ConfigureAwait(false);

        }).ConfigureAwait(false);

    });

});

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
