var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedisContainer("cache");

var rabbitmq = builder.AddRabbitMQContainer("rabbitmq");

var init = builder.AddProject<Projects.DotNetAspire_AppInit>("init")    
    .WithReference(cache);

var backend = builder.AddProject<Projects.DotNetAspire_BackendHost>("backend")
    .WithReference(rabbitmq)
    .WithReference(init);

builder.AddProject<Projects.DotNetAspire_FrontEndHost>("webfrontend")
    .WithReference(init)
    .WithReference(cache)
    .WithReference(backend);



builder.Build().Run();

Console.WriteLine("AspireHost");