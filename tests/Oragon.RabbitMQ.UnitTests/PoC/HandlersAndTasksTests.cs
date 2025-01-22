// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;

namespace Oragon.RabbitMQ.UnitTests.PoC;

public class SampleRequest
{
    public object ReturnValue { get; set; }

    public bool ThrowException { get; set; }
}

public interface ISampleResponse
{
    object ReturnValue { get; }
}
public class SampleResponse : ISampleResponse
{
    public required object ReturnValue { get; set; }
}

public class SampleService
{
    public void Teste1(SampleRequest sampleData)
    {
        if (sampleData.ThrowException) throw new InvalidOperationException("Teste1");
    }
    public Task Teste2Async(SampleRequest sampleData)
    {
        if (sampleData.ThrowException) throw new InvalidOperationException("Teste2Async");
        return Task.CompletedTask;
    }

}

public class HandlersAndTasksTests
{


    [Fact]
    public async Task TestAfterDispatchBehaviors()
    {
        SampleService svc = new SampleService();

        List<Delegate> delegates =
        [
            //---------------------------------------------------------------------------------------------------------------
            ([FromServices] SampleService svc, SampleRequest msg) => svc.Teste1(msg),
            //---------------------------------------------------------------------------------------------------------------

            //---------------------------------------------------------------------------------------------------------------
            async ([FromServices] SampleService svc, SampleRequest msg) => await svc.Teste2Async(msg).ConfigureAwait(true),
            //---------------------------------------------------------------------------------------------------------------

            //---------------------------------------------------------------------------------------------------------------
            async ([FromServices] SampleService svc, SampleRequest msg) => {
                await svc.Teste2Async(msg).ConfigureAwait(true);
                return AmqpResults.Reject(false);
            },
            //---------------------------------------------------------------------------------------------------------------

            //---------------------------------------------------------------------------------------------------------------
            async ([FromServices] SampleService svc, SampleRequest msg) => {
                await svc.Teste2Async(msg).ConfigureAwait(true);
                return "Teste";
            },
            //---------------------------------------------------------------------------------------------------------------

            //---------------------------------------------------------------------------------------------------------------
            ([FromServices] SampleService svc, SampleRequest msg) => {
                svc.Teste1(msg);
                return "Teste";
            },
            //---------------------------------------------------------------------------------------------------------------

            //---------------------------------------------------------------------------------------------------------------
            ([FromServices] SampleService svc, SampleRequest msg) => {
                svc.Teste1(msg);
                return AmqpResults.Reject(false);
            },
            //---------------------------------------------------------------------------------------------------------------
        ];

        var types = delegates.Select(it => it.Method.ReturnType).ToList();

        foreach (var currentDelegate in delegates)
        {
            await DispatchWithSuccess(currentDelegate);
            await DispatchForFailure(currentDelegate);
        }

    }

    private static async Task DispatchWithSuccess(Delegate delegateToHandle)
    {
        Mock<IServiceProvider> serviceProviderMock = new Mock<IServiceProvider>();
        _ = serviceProviderMock.Setup(it => it.GetService(typeof(SampleService))).Returns(new SampleService());

        Mock<IAmqpContext> amqpContextMock = new Mock<IAmqpContext>();
        _ = amqpContextMock.Setup(it => it.MessageObject).Returns(new SampleRequest() { ReturnValue = 1 });
        _ = amqpContextMock.Setup(it => it.ServiceProvider).Returns(serviceProviderMock.Object);

        var queueConsumerBuilder = new ConsumerParameters(serviceProviderMock.Object, "oragon-rabbitmq-queueName", delegateToHandle);

        var dispatcher = new Dispatcher(queueConsumerBuilder);

        IAmqpResult amqpResult = await dispatcher.DispatchAsync(amqpContextMock.Object).ConfigureAwait(false);

        Type[] typesOk = [typeof(AckResult), typeof(RejectResult)];
        Type[] typesNOk = [typeof(NackResult), typeof(ReplyResult<>)];

        Assert.Contains(amqpResult.GetType(), typesOk);
        Assert.DoesNotContain(amqpResult.GetType(), typesNOk);
    }

    private static async Task DispatchForFailure(Delegate delegateToHandle)
    {
        Mock<IServiceProvider> serviceProviderMock = new Mock<IServiceProvider>();
        _ = serviceProviderMock.Setup(it => it.GetService(typeof(SampleService))).Returns(new SampleService());

        Mock<IAmqpContext> amqpContextMock = new Mock<IAmqpContext>();
        _ = amqpContextMock.Setup(it => it.MessageObject).Returns(new SampleRequest() { ReturnValue = 1, ThrowException = true });
        _ = amqpContextMock.Setup(it => it.ServiceProvider).Returns(serviceProviderMock.Object);

        var queueConsumerBuilder = new ConsumerParameters(serviceProviderMock.Object, "oragon-rabbitmq-queueName", delegateToHandle);

        var dispatcher = new Dispatcher(queueConsumerBuilder);

        IAmqpResult amqpResult = await dispatcher.DispatchAsync(amqpContextMock.Object).ConfigureAwait(false);

        Type[] typesOk = [typeof(NackResult)];
        Type[] typesNOk = [typeof(AckResult), typeof(RejectResult), typeof(ReplyResult<>)];

        Assert.Contains(amqpResult.GetType(), typesOk);
        Assert.DoesNotContain(amqpResult.GetType(), typesNOk);
    }


}
