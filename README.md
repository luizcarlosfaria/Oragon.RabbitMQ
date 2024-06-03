
# Oragon.RabbitMQ 

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=bugs)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=coverage)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=Oragon.RabbitMQ&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=Oragon.RabbitMQ)
![GitHub last commit](https://img.shields.io/github/last-commit/luizcarlosfaria/Oragon.RabbitMQ)

![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white)
![.Net](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![Visual Studio](https://img.shields.io/badge/Visual%20Studio-5C2D91.svg?style=for-the-badge&logo=visual-studio&logoColor=white)

![Jenkins](https://img.shields.io/badge/jenkins-%232C5263.svg?style=for-the-badge&logo=jenkins&logoColor=white)

[![Telegram](https://img.shields.io/badge/Telegram-2CA5E0?style=for-the-badge&logo=telegram&logoColor=white) 🇧🇷 ](https://t.me/luizcarlosfaria)

Opinionated and Simplified Minimal APIs for Consuming Messages from RabbitMQ, Ensuring No Crucial Configurations Are Hidden.

## What is Oragon.RabbitMQ?

Oragon.RabbitMQ provides everything you need to create resilient RabbitMQ consumers without the need to study numerous books and articles or introduce unknown risks to your environment.

### If you have a service like this
```cs
public class BusinessService
{
    public async Task DoSomethingAsync(BusinessCommandOrEvent commandOrEvent)
    {
        ... business core ...
    }
}
```

### You will create a RabbitMQ Consumers with this

#### Singleton
```cs
builder.Services.AddSingleton<BusinessService>();

builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

builder.Services.MapQueue<BusinessService, BusinessCommandOrEvent>(config => config
    .WithDispatchInRootScope()    
    .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
    .WithQueueName("events")
    .WithPrefetchCount(1)
);

```

#### Scoped
```cs
builder.Services.AddScoped<BusinessService>();

builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

builder.Services.MapQueue<BusinessService, BusinessCommandOrEvent>(config => config
    .WithDispatchInChildScope()    
    .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
    .WithQueueName("events")
    .WithPrefetchCount(1)
);

```

#### Scoped and Keyed Services
```cs
builder.Services.AddKeyedScoped<BusinessService>("key-of-service-1");
builder.Services.AddKeyedScoped("key-of-service-2", (sp, key) => new BusinessService(... custom dependencies ...));

builder.Services.AddSingleton<IAMQPSerializer, SystemTextJsonAMQPSerializer>();

builder.Services.MapQueue<BusinessService, BusinessCommandOrEvent>(config => config
    .WithDispatchInChildScope()
    .WithKeyedService("key-of-service-1") // or "key-of-service-2"
    .WithAdapter((svc, msg) => svc.DoSomethingAsync(msg))
    .WithQueueName("events")
    .WithPrefetchCount(1)
);

```

# Concepts

## Decoupling Business Logic from Infrastructure

This approach is designed to decouple RabbitMQ consumers from business logic, ensuring that business code remains unaware of the queue consumption context.

The result is incredibly simple, decoupled, agnostic, more reusable, and highly testable code.

## Opinionated Design: Why?

This consumer is focused on creating a resilient consumer using manual acknowledgments.

-   The flow produces a `BasicReject` without requeue for serialization failures (e.g., incorrectly formatted messages),  you will use dead-lettering to ensure these messages are not lost.
-  The flow produces a `BasicNack` with requeue for processing failures, allowing for message reprocessing.
- Minimal API design style made without reflection
- Extensible with support for custom serializers and encoders

# RabbitMQ Tracing com OpenTelemetry

Full support for OpenTelemetry on **publishing** or **consuming** RabbitMQ messages.

<img src="./docs/playground.gif">


Refactored to use RabbitMQ.Client 7x (with IChannel instead IModel)

