using Aspire.Hosting.Lifecycle;
using GagoAspireApp.AppHost;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedisContainer("cache");

var rabbitmq = builder.AddRabbitMQContainer("rabbitmq");

var init = builder.AddProject<Projects.GagoAspireApp_AppInit>("init")    
    .WithReference(cache);

var apiservice = builder.AddProject<Projects.GagoAspireApp_ApiService>("apiservice")
    .WithReference(rabbitmq)
    .WithReference(init);

builder.AddProject<Projects.GagoAspireApp_Web>("webfrontend")
    .WithReference(init)
    .WithReference(cache)
    .WithReference(apiservice);



builder.Services.AddTransient<IDistributedApplicationLifecycleHook, RabbitMQSetup>();

builder.Build().Run();

Console.WriteLine("AspireHost");