using Aspire.Hosting.Lifecycle;
using AspireApp1.AppHost;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedisContainer("cache");

var rabbitmq = builder.AddRabbitMQContainer("rabbitmq");

var init = builder.AddProject<Projects.AspireApp1_AppInit>("init")
    .WithReference(rabbitmq)
    .WithReference(cache);

var apiservice = builder.AddProject<Projects.AspireApp1_ApiService>("apiservice")
    .WithReference(init)
    .WithReference(rabbitmq);

builder.AddProject<Projects.AspireApp1_Web>("webfrontend")
    .WithReference(init)
    .WithReference(cache)
    .WithReference(apiservice);



builder.Services.AddTransient<IDistributedApplicationLifecycleHook, RabbitMQSetup>();

builder.Build().Run();

Console.WriteLine("AspireHost");