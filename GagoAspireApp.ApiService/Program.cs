using GagoAspireApp.Architecture.Messaging;
using GagoAspireApp.Architecture.Messaging.Serialization;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<BusinessService>();
builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();



builder.Services.MapQueue<BusinessService, BusinessCommandOrEvent>(config => config
    
    .WithDispatchInRootScope()
    .WithAdapter(async (svc, msg) => await svc.DoSomethingAsync(msg))
    .WithQueueName("events")
    .WithPrefetchCount(1)
    .WithTopology((sp, model) => {
        model.QueueDeclare("events");
    })
);

builder.AddRabbitMQ("rabbitmq", null, cf => { ((ConnectionFactory)cf).DispatchConsumersAsync = true; });

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

app.MapPost("/enqueue", (BusinessCommandOrEvent msg, [FromServices] IConnection connection) =>
{
    using var model = connection.CreateModel();

    string msgText = Newtonsoft.Json.JsonConvert.SerializeObject(msg);

    var bytes = System.Text.Encoding.UTF8.GetBytes(msgText);

    model.BasicPublish("", "events", model.CreateBasicProperties(), bytes);

    Console.WriteLine(msg.ItemId);
});






app.MapDefaultEndpoints();

app.Run();

Console.WriteLine("ApiService");

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}



public record BusinessCommandOrEvent(string ItemId)
{
}

public class BusinessService
{
    public Task DoSomethingAsync(BusinessCommandOrEvent commandOrEvent)
    {
        Console.WriteLine(commandOrEvent.ItemId);

        return Task.CompletedTask;
    }
}