// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

var username = builder.AddParameter("rabbitmq-username");
var password = builder.AddParameter("rabbitmq-password", secret: true);


var rabbitmq = builder.AddRabbitMQ("rabbitmq", username, password)
    .WithImage("library/rabbitmq", "4-management")
    //.WithManagementPlugin()
    .WithHttpEndpoint(port: null, targetPort: 15672, name: "management")
    ;

var apiService = builder.AddProject<Projects.DotNetAspireApp_ApiService>("apiservice")
    .WithReference(rabbitmq)
#if NET9_0_OR_GREATER
    .WaitFor(rabbitmq)
#endif
    ;

builder.AddProject<Projects.DotNetAspireApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(redis)
    .WithReference(apiService)
#if NET9_0_OR_GREATER
    .WaitFor(apiService)
#endif
    ;

builder.AddProject<Projects.DotNetAspireApp_Worker>("worker")
    .WithReference(rabbitmq)
#if NET9_0_OR_GREATER
    .WaitFor(rabbitmq)
#endif
    .WithReplicas(4)
    ;

builder.Build().Run();
