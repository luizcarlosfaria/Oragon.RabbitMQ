using DotNetAspire.Architecture.Aspire;
using DotNetAspire.Architecture.Messaging;
using DotNetAspire.Architecture.Messaging.Publisher;
using DotNetAspire.Architecture.Messaging.Serialization;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp => new ActivitySource("RabbitMQ.Gago", "1.0.0"));



// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<BusinessService>();
builder.Services.AddSingleton<MessagePublisher>();
builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

builder.Services.MapQueue<BusinessService, BusinessCommandOrEvent>(config => config
    .WithDispatchInRootScope()    
    .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
    .WithQueueName("events")
    .WithPrefetchCount(1)
    .WithTopology((sp, model) => model.QueueDeclare("events"))
);

builder.AddRabbitMQ("rabbitmq", null, cf => cf.Unbox().DispatchConsumersAsync());


// Add service defaults & Aspire components.
builder.AddServiceDefaults();

var app = builder.Build();


// Configure the HTTP request pipeline.
app.UseExceptionHandler();


string[] summaries =
[
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
];

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.MapPost("/enqueue", (BusinessCommandOrEvent msg, [FromServices] MessagePublisher messagePublisher)
    => messagePublisher.Send("", "events", msg));



app.MapDefaultEndpoints();

app.Run();

Console.WriteLine("backend");

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}



public record BusinessCommandOrEvent(string ItemId)
{
}

public class BusinessService
{
    public async Task DoSomethingAsync(BusinessCommandOrEvent commandOrEvent)
    {
        Console.WriteLine($"Consumer Recebeu | {commandOrEvent.ItemId}");

        await Task.Delay(5000);
    }
}

public class BusinessService2
{
    public async Task<BusinessCommandOrEvent> DoSomething2Async(BusinessCommandOrEvent commandOrEvent)
    {
        Console.WriteLine($"Consumer Recebeu | {commandOrEvent.ItemId}");

        await Task.Delay(5000);

        return commandOrEvent;
    }
}