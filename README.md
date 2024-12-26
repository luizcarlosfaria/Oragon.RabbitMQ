
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

### Implement your service
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
If you are using **.NET Aspire**, you can use .NET Aspire with:

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

    app.MapQueue("queueName", ([FromServices] BusinessService svc, BusinessCommandOrEvent msg) => 
        svc.DoSomethingAsync(msg));

    ```
    
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

app.MapQueue("queueName", ([FromServices] BusinessService svc, BusinessCommandOrEvent msg) => 
    svc.DoSomethingAsync(msg));

app.Run();

```
# Concepts

## Decoupling Business Logic from Infrastructure

This approach is designed to decouple RabbitMQ consumers from business logic, ensuring that business code remains unaware of the queue consumption context.

The result is incredibly simple, decoupled, agnostic, more reusable, and highly testable code.

## Opinionated Design: Why?

This consumer is focused on creating a resilient consumer using manual acknowledgments.

-  The automatic flow produces a `BasicReject` without requeue when serialization failures (e.g., incorrectly formatted messages),  you must use dead-lettering to ensure that your message will not be lost.
-  The automatic flow produces a `BasicNack` without requeue for processing failures. You must use dead-lettering to ensure that your message will not be lost.
- Minimal API design style made without reflection
- Extensible with support for custom serializers and encoders

## Flexible

### AMQP Flow Control

Autoflow use Ack, Nack and Reject automatically, but you can control the flow.

Inside `Oragon.RabbitMQ.Consumer.Actions` namespace you can find some results:
- AckResult (`new AckResult();`)
- ComposableResult (`new ComposableResult(params IAMQPResult[] results);`)
- NackResult (`new NackResult(bool requeue);`)
- RejectResult (`new RejectResult(bool requeue);`)
- ReplyResult (`new ReplyResult(object objectToReply);`) ⚠️EXPERIMENTAL⚠️

Above you can see how it's can work for you

```cs
app.MapQueue("queueName", ([FromServices] BusinessService svc, BusinessCommandOrEvent msg) => {
    
    IAMQPResult returnValue;

    if (svc.CanXpto(msg))
    {
        svc.DoXpto(msg);

        returnValue = new AckResult();

    } else {

        returnValue = new RejectResult(requeue: true);

    }
    return returnValue;
})
.WithPrefetch(2000)
.WithDispatchConcurrency(4);
```
### Async os No
```cs
app.MapQueue("queueName", async ([FromServices] BusinessService svc, BusinessCommandOrEvent msg) => {

    if (await svc.CanXpto(msg))
    {
       await svc.DoXpto(msg);

       return new AckResult();

    } else {

        return new RejectResult(requeue: true);
        
    }
})
.WithPrefetch(2000)
.WithDispatchConcurrency(4);
```
### Model Binder Examples

### Special Types

For this types, model binder will set correct current instance without need a special attribute.

- RabbitMQ.Client.IConnection
- RabbitMQ.Client.IChannel
- RabbitMQ.Client.Events.BasicDeliverEventArgs
- RabbitMQ.Client.DeliveryModes
- RabbitMQ.Client.IReadOnlyBasicProperties
- System.IServiceProvider (scoped)

### Special Names

For this names, model binder will use a name to set a set correct current string from consumer.

#### QueueName
The model binder will set a queue that the consumer are consuming.
- queue
- queueName

#### Routing Key
The model binder will set a routing key from the amqp message.
- routing
- routingKey

#### Exchange Name
The model binder will set a exchange name from the amqp message.
- exchange
- exchangeName


#### Consumer Tag
The model binder will set a consumer tag from the actual consumer.
- consumer
- consumerTag





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
- [x] Review All SuppressMessageAttribute
- [x] Create Docs
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
- [x] Change original behavior based on lambda expressions to dynamic delegate.

