using System.Diagnostics;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ;
using RabbitMQ.Client;
using DotNetAspireApp.Common.Messages.Commands;
using DotNetAspireApp.Worker.Areas;
using Microsoft.AspNetCore.Connections;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp => new ActivitySource("RabbitMQ.Gago", "1.0.0"));
builder.AddRabbitMQClient("rabbitmq", null, connectionFactory =>
{
    connectionFactory.DispatchConsumersAsync = true;
    connectionFactory.ConsumerDispatchConcurrency = DotNetAspireApp.Worker.Constants.Parallelism;
    //connectionFactory.TopologyRecoveryEnabled = true;
    //connectionFactory.AutomaticRecoveryEnabled = true;
    connectionFactory.ClientProvidedName = "DotNetAspireApp.Worker";
});

builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

builder.Services.AddEmailService();

builder.AddServiceDefaults();

var app = builder.Build();

//await app.Services.WaitRabbitMQAsync().ConfigureAwait(false);

app.Run();
