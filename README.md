# ASPIRE / RabbitMQ Minimal API's 
## Demos

Demonstração do uso do ASPIRE em ambiente de desenvolvimento.


# RabbitMQ Minimal API's

Uma vez que temos uma classe de negócio assim:
```cs
public class BusinessService
{
    public async Task DoSomethingAsync(BusinessCommandOrEvent commandOrEvent)
    {
        Console.WriteLine($"Consumer Recebeu | {commandOrEvent.ItemId}");

        await Task.Delay(5000);
    }
}
```

podemos conectar um método à uma fila assim:

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

Essa é uma abordagem projetada para desacoplar o consumidor do RabbitMQ do código de negócio, forçando com que o código de negócio não saiba que está em um contexto de consumo de filas.

Essa abordagem intensionalmente remove a capacidade de utilização de notification pattern para a rejeição de mensagens, fazendo com que necessariamente seja lançada uma exceção, de tal forma que permita ao administrador da infraestrutura de observabilidade, ser notificado claramente quando os processos falham, permitindo assim a criação de issues para correção no código, ao invés de omitir e suprimir erros.

O resultado é um código absurdamente simples, desacoplado, agnóstico, mais reaproveitável e altamente testável.

# RabbitMQ Tracing com OpenTelemetry

Suporte completo para OpenTelemetry na **publicação** e no **consumo** de mensagens do RabbitMQ.

<img src="./docs/playground.gif">


