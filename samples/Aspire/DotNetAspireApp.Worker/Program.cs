using System.Diagnostics;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ;
using DotNetAspireApp.Worker;
using RabbitMQ.Client;
using DotNetAspireApp.Common.Messages.Commands;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp => new ActivitySource("RabbitMQ.Gago", "1.0.0"));

//builder.Services.AddSingleton<MessagePublisher>();

builder.Services.AddSingleton<BusinessService1>();
builder.Services.AddSingleton<BusinessService2>();
builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

builder.Services.MapQueue<BusinessService1, DoSomethingCommand>(config => config
    .WithDispatchInRootScope()
    .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
    .WithQueueName("events")
    .WithPrefetchCount(1)
    .WithTopology((sp, channel) => channel.QueueDeclareAsync("events", durable: true, exclusive: false, autoDelete: false))
);

builder.AddRabbitMQClient("rabbitmq", null, cf => cf.DispatchConsumersAsync());

builder.AddServiceDefaults();


var app = builder.Build();

app.Run();
