# Oragon.RabbitMQ 

An opinionated and simplest minimal APIs for consuming messages from RabbitMQ, without hidden important configurations.

## What is Oragon.RabbitMQ?
Oragon.RabbitMQ delivery anything that you need to create resilient RabbitMQ Consumers without need to understand or read many books and posts, or add unknown risks to your environment.

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

# Concepts
This is an approach designed to decouple the RabbitMQ consumer from the business code, forcing the business code to not know that it is in a queue consumption context.

The result is absurdly simple, decoupled, agnostic, more reusable and highly testable code.


# RabbitMQ Tracing com OpenTelemetry

Full support for OpenTelemetry on **publishing** or **consuming** RabbitMQ messages.


<img src="./docs/playground.gif">

# Others

Refactored to use RabbitMQ.Client 7x (with IChannel instead IModel)

