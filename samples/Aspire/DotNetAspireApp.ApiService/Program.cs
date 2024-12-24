using System.Reflection;
using System.Text.Json;
using DotNetAspireApp.Common.Messages.Commands;
using Microsoft.AspNetCore.Mvc;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.AspireClient;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = WebApplication.CreateBuilder(args);


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


app.MapPost("/enqueue", (DoSomethingRequest req, CancellationToken cancellationToken, [FromServices] IAMQPSerializer serializer, [FromServices] IConnectionFactory connectionFactory)
    =>
{
    _ = Task.Run(async () =>
    {
        using var connection = await connectionFactory.CreateConnectionAsync("ApiService - enqueue", CancellationToken.None).ConfigureAwait(false);
        using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);



        bool isBlocked = false;

        connection.ConnectionBlockedAsync += delegate (object sender, ConnectionBlockedEventArgs @event)
        {
            Volatile.Write(ref isBlocked, true);
            return Task.CompletedTask;
        };
        connection.ConnectionUnblockedAsync += delegate (object sender, AsyncEventArgs @event)
        {
            Volatile.Write(ref isBlocked, false);
            return Task.CompletedTask;
        };

        for (int i = 1; i <= req.quantity; i++)
        {
            for (int retryWait = 0; Volatile.Read(ref isBlocked) && retryWait < 90; retryWait++)
            {
                Console.WriteLine("Connection is blocked. Waiting... ");
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                if (!Volatile.Read(ref isBlocked)) break;
            }

            var command = new DoSomethingCommand(req.Text, i, req.quantity);

            var properties = channel.CreateBasicProperties().EnsureHeaders().SetDurable(true);

            var body = serializer.Serialize(basicProperties: properties, message: command);

            await channel.BasicPublishAsync("events", string.Empty, false, properties, body).ConfigureAwait(false);

        };

        await channel.CloseAsync().ConfigureAwait(false);
        await connection.CloseAsync().ConfigureAwait(false);

    });

    return "ok";
});


app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
