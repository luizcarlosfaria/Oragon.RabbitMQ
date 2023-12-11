using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedisContainer("cache");

var rabbitmq = builder.AddRabbitMQContainer("rabbitmq");

var init = builder.AddProject<Projects.GagoAspireApp_AppInit>("init")    
    .WithReference(cache);

var backend = builder.AddProject<Projects.GagoAspireApp_BackendHost>("backend")
    .WithReference(rabbitmq)
    .WithReference(init);

builder.AddProject<Projects.GagoAspireApp_FrontEndHost>("webfrontend")
    .WithReference(init)
    .WithReference(cache)
    .WithReference(backend);



builder.Build().Run();

Console.WriteLine("AspireHost");