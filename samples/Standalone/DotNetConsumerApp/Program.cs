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

//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//Choose one of the following serializers
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//builder.Services.AddSingleton<IAMQPSerializer>(sp => new SystemTextJsonAMQPSerializer(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.General)));
builder.Services.AddSingleton<IAMQPSerializer>(sp => new NewtonsoftAMQPSerializer(new Newtonsoft.Json.JsonSerializerSettings()));

builder.Services.MapQueue<UpdateService, PriceChangeEvent>(config => config
    .WithDispatchInChildScope()
    .WithRequeueOnCrash(false)
    .WithAdapter((svc, msg) => svc.UpdatePriceCacheAsync(msg))
    .WithQueueName("events1")
    .WithPrefetchCount(1)
    .WithTopology(async (serviceProvider, channel) =>
    {
        _ = await channel.QueueDeclareAsync("events1_dl",
            durable: true,
            autoDelete: false,
            exclusive: false)
        .ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync("events1",
            durable: true,
            autoDelete: false,
            exclusive: false,
            arguments: new Dictionary<string, object>()
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", "events1_dl" },
            })
        .ConfigureAwait(false);
    })
);

builder.Services.MapQueue<UpdateService, PriceChangeEvent>(config => config
    .WithDispatchInChildScope()
    .WithRequeueOnCrash(false)
    .WithAdapter((svc, msg) => svc.UpdatePriceCacheAsync(msg))
    .WithQueueName("events2")
    .WithPrefetchCount(1)
    .WithTopology(async (serviceProvider, channel) =>
    {
        _ = await channel.QueueDeclareAsync("events2_dl",
            durable: true,
            autoDelete: false,
            exclusive: false)
        .ConfigureAwait(false);

        _ = await channel.QueueDeclareAsync("events2",
            durable: true,
            autoDelete: false,
            exclusive: false,
            arguments: new Dictionary<string, object>()
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", "events2_dl" },
            })
        .ConfigureAwait(false);
    })
);

builder.Services.MapQueue<UpdateService, PriceChangeEvent>(config => config
    .WithDispatchInChildScope()
    .WithRequeueOnCrash(true) //produce requeue forever when failure forever
    .WithAdapter((svc, msg) => svc.UpdatePriceCacheAsync(msg))
    .WithQueueName("events3")
    .WithPrefetchCount(1)
    .WithTopology((serviceProvider, channel) => channel.QueueDeclareAsync("events3"))
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
