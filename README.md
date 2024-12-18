
![Card](https://raw.githubusercontent.com/luizcarlosfaria/Oragon.RabbitMQ/master/src/Assets/opengraph-card.png) 

# Oragon.RabbitMQ 

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=alert_status)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=bugs)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=code_smells)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=coverage)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=duplicated_lines_density)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=reliability_rating)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=security_rating)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=sqale_index)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=sqale_rating)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=vulnerabilities)](https://sonarcloud.io/summary/overall?id=Oragon.RabbitMQ)
[![GitHub last commit](https://img.shields.io/github/last-commit/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ/commits/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Oragon.RabbitMQ)](https://www.nuget.org/packages/Oragon.RabbitMQ/) [![GitHub Repo stars](https://img.shields.io/github/stars/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ)


[![Roadmap](https://img.shields.io/badge/Roadmap-%23ff6600?logo=github&logoColor=%23000000&label=GitHub&labelColor=%23f0f0f0)](https://github.com/users/luizcarlosfaria/projects/3/views/3)




## Official Release 

[![NuGet Version](https://img.shields.io/nuget/v/Oragon.RabbitMQ?logo=nuget&label=nuget)](https://www.nuget.org/packages?q=Oragon.RabbitMQ&includeComputedFrameworks=true&prerel=true&sortby=created-desc)

## Others

[![GitHub Tag](https://img.shields.io/github/v/tag/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ/tags)

[![GitHub Release](https://img.shields.io/github/v/release/luizcarlosfaria/Oragon.RabbitMQ)](https://github.com/luizcarlosfaria/Oragon.RabbitMQ/releases)

[![MyGet Version](https://img.shields.io/myget/oragon/vpre/Oragon.RabbitMQ?logo=myget&label=myget)](https://www.myget.org/feed/Packages/oragon)

## Tech / Skill

![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white)
![.Net](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![Visual Studio](https://img.shields.io/badge/Visual%20Studio-5C2D91.svg?style=for-the-badge&logo=visual-studio&logoColor=white)

[![Jenkins](https://img.shields.io/badge/jenkins-%232C5263.svg?style=for-the-badge&logo=jenkins&logoColor=white)](https://jenkins.oragon.io/job/oragon/job/Oragon.RabbitMQ/)

[![Telegram](https://img.shields.io/badge/Telegram-2CA5E0?style=for-the-badge&logo=telegram&logoColor=white)](https://t.me/luizcarlosfaria)

Opinionated and Simplified Minimal APIs for Consuming Messages from RabbitMQ, Ensuring No Crucial Configurations Are Hidden.

# What is Oragon.RabbitMQ?

## Oragon.RabbitMQ is a Minimal API implementation for Consume RabbitMQ Queues.

Oragon.RabbitMQ provides everything you need to create resilient RabbitMQ consumers without the need to study numerous books and articles or introduce unknown risks to your environment.

## Get Started

#### Add Consumer and Serializer packages
```bash
dotnet add package Oragon.RabbitMQ
dotnet add package Oragon.RabbitMQ.Serializer.SystemTextJson
```

### Implement your service (with or without interface)
```cs
public class BusinessService
{
    public async Task DoSomethingAsync(BusinessCommandOrEvent commandOrEvent)
    {
        ... business core ...
    }
}
```

### Configuring Dependency Injection

#### Basic Setup
```cs
var builder = WebApplication.CreateBuilder(args); //or Host.CreateApplicationBuilder(args);

.../*your dependencies configuration*/...

builder.AddRabbitMQConsumer();

builder.Services.AddSingleton<BusinessService>();

builder.Services.AddSingleton<IAMQPSerializer>(sp => new SystemTextJsonAMQPSerializer(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.General){ ... }));
```


#### Inject a valid IConnection on dependency injection
The consumer will use the dependency injection to get an valid instance of **RabbitMQ.Client.IConnection**. If you does not provider one, above you can see how to create a connection configuration.

```cs
...
builder.Services.AddSingleton<IConnectionFactory>(sp => new ConnectionFactory()
{
    Uri = new Uri("amqp://rabbitmq:5672"),
    DispatchConsumersAsync = true
});

builder.Services.AddSingleton(sp => sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());
...

```
If you are using .NET Aspire, you can use .NET Aspire with:

```cs
...
builder.AddRabbitMQClient("rabbitmq");
...

```

# 🎯 Map your Queue 🎯

To map your queue using this package, follow these steps:

1. **Build the application:**
    First, you need to build your application using the builder pattern. This initializes the application and prepares it for further configuration.
    ```cs
    var app = builder.Build();
    ```

2. **Map the queue:**
    Next, map your queue to a specific service and command/event. This step involves configuring how the service will handle incoming messages from the queue.
    ```cs
    app.MapQueue<BusinessService, BusinessCommandOrEvent>(config => config
        .WithDispatchInRootScope()  // Use for singleton service
        .WithDispatchInChildScope() // Use for scoped service
        .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg)) // Define how the service handles the message
        .WithQueueName("events") // Set the queue name
        .WithPrefetchCount(System.Environment.ProcessorCount * 16 * 10) // Set the prefetch count
    );
    ```
    - `WithDispatchInRootScope()`: Use this for singleton services that should be instantiated once and shared across the application.
    - `WithDispatchInChildScope()`: Use this for scoped services that should be instantiated per request or per message.
    - `WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))`: Define how the service handles the incoming message. This is where you specify the method to process the message.
    - `WithQueueName("events")`: Set the name of the queue from which the messages will be consumed.
    - `WithPrefetchCount(System.Environment.ProcessorCount * 16 * 10)`: Set the prefetch count to control how many messages the consumer can fetch at a time. This can help optimize performance.

3. **Run the application:**
    Finally, run the application to start processing messages from the queue.
    ```cs
    app.Run();
    ```
    

---

## Full Example
```cs

var builder = WebApplication.CreateBuilder(args);

builder.AddRabbitMQConsumer();

builder.Services.AddSingleton<BusinessService>();

builder.Services.AddSingleton<IAMQPSerializer>(sp => new SystemTextJsonAMQPSerializer(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.General){ ... }));

builder.Services.AddSingleton<IConnectionFactory>(sp => new ConnectionFactory(){ Uri = new Uri("amqp://rabbitmq:5672"), DispatchConsumersAsync = true });

builder.Services.AddSingleton(sp => sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync().GetAwaiter().GetResult());

var app = builder.Build();

app.MapQueue<BusinessService, BusinessCommandOrEvent>(config => config
    .WithDispatchInRootScope()  // ->  for singleton service
    .WithDispatchInChildScope() // ->  for scoped service
    .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
    .WithQueueName("queue")
    .WithPrefetchCount(System.Environment.ProcessorCount * 16 * 10)
);

app.Run();

```



# Concepts

## Decoupling Business Logic from Infrastructure

This approach is designed to decouple RabbitMQ consumers from business logic, ensuring that business code remains unaware of the queue consumption context.

The result is incredibly simple, decoupled, agnostic, more reusable, and highly testable code.

## Opinionated Design: Why?

This consumer is focused on creating a resilient consumer using manual acknowledgments.

-  The flow produces a `BasicReject` without requeue for serialization failures (e.g., incorrectly formatted messages),  you will use dead-lettering to ensure these messages are not lost.
-  The flow produces a `BasicNack` with requeue for processing failures, allowing for message reprocessing.
- Minimal API design style made without reflection
- Extensible with support for custom serializers and encoders

# RabbitMQ Tracing com OpenTelemetry

Full support for OpenTelemetry on **publishing** or **consuming** RabbitMQ messages.

<img src="./docs/playground.gif">


Refactored to use RabbitMQ.Client 7x (with IChannel instead IModel)


## Stages and Requirements for Launch 
- [x] Migrate Demo to Library Project
- [x] Core: Queue Consumer
- [x] Core: Rpc Queue Consumer
- [x] Core: Support Keyed Services
- [x] Core: Support of new design of RabbitMQ.Client
- [x] Create Samples
- [ ] Review All SuppressMessageAttribute
- [ ] Create Docs
- [ ] Benchmarks
- [x] Automate Badges
- [x] Add SonarCloud
- [x] Code Coverage > 80%
- [X] Add CI/CD
- [x] Add Unit Tests
- [x] Add Integrated Tests with TestContainers
- [x] Test CI/CD Flow: MyGet Alpha Packages with Symbols
- [x] Test CI/CD Flow: MyGet Packages without Symbols
- [x] Test CI/CD Flow: Nuget Packages without Symbols


