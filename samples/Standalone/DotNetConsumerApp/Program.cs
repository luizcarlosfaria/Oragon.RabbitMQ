// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton<IConnectionFactory>(sp => new ConnectionFactory()
{
    Uri = new Uri("amqp://rabbitmq:5672"),
    DispatchConsumersAsync = true
});

builder.Services.AddSingleton(sp => sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());


builder.Services.AddScoped<UpdateService>();
builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

builder.Services.MapQueue<UpdateService, PriceChangeEvent>(config => config
    .WithDispatchInChildScope()
    .WithAdapter((svc, msg) => svc.UpdatePriceCacheAsync(msg))
    .WithQueueName("events1")
    .WithPrefetchCount(1)
    .WithTopology((serviceProvider, channel) => channel.QueueDeclareAsync("events1", durable: true, autoDelete: false, exclusive: false))
);

builder.Services.MapQueue<UpdateService, PriceChangeEvent>(config => config
    .WithDispatchInChildScope()
    .WithAdapter((svc, msg) => svc.UpdatePriceCacheAsync(msg))
    .WithQueueName("events2")
    .WithPrefetchCount(1)
    .WithTopology((serviceProvider, channel) => channel.QueueDeclareAsync("events2", durable: true, autoDelete: false, exclusive: false))
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

#region Web Minimal API Stuff

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
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

#endregion
