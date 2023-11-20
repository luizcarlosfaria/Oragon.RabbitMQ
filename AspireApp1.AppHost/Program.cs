using Aspire.Hosting.Lifecycle;
using AspireApp1.AppHost;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedisContainer("cache");

var rabbitmq = builder.AddRabbitMQContainer("rabbitmq");

var apiservice = builder.AddProject<Projects.AspireApp1_ApiService>("apiservice")
    .WithReference(rabbitmq);

builder.AddProject<Projects.AspireApp1_Web>("webfrontend")
    .WithReference(cache)
    .WithReference(apiservice);

builder.Services.AddTransient<IDistributedApplicationLifecycleHook, RabbitMQSetup>();

builder.Build().Run();
