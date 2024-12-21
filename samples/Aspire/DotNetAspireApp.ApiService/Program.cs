using System;
using System.Text.Json;
using System.Threading.Channels;
using DotNetAspireApp.Common.Messages.Commands;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;
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

app.MapPost("/weatherforecast", ([FromServices] IConnectionFactory connectionFactory, string nome) =>
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


app.MapPost("/enqueue", (DoSomethingRequest req, CancellationToken cancellationToken, [FromServices] IAMQPSerializer serializer, [FromServices] IConnectionFactory connectionFactory)
    =>
{
    _ = Task.Run(async () =>
    {
        using var connection = await connectionFactory.CreateConnectionAsync("ApiService - enqueue", CancellationToken.None).ConfigureAwait(true);
        using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(true);

        for (int i = 1; i <= req.quantity; i++)
        {

            var command = new DoSomethingCommand(req.Text, i, req.quantity);

            var properties = channel.CreateBasicProperties().EnsureHeaders().SetDurable(true);

            var body = serializer.Serialize(basicProperties: properties, message: command);

            await channel.BasicPublishAsync("events", string.Empty, false, properties, body).ConfigureAwait(true);

        };
    });

    return "ok";
});

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
