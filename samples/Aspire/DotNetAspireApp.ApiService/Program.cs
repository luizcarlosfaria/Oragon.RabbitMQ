using DotNetAspireApp.Common.Messages.Commands;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Publisher;
using Oragon.RabbitMQ.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<MessagePublisher>();
builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

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

app.MapPost("/enqueue", async (DoSomethingRequest req, [FromServices] MessagePublisher messagePublisher)
    =>
{
    await Parallel.ForAsync(1,req.quantity, async (i, ct) =>
    {
        await messagePublisher.SendAsync("", "events", req, ct).ConfigureAwait(false);
    }).ConfigureAwait(false);

});

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
