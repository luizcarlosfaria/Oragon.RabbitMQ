// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch;

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
                return new RejectResult(false);
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
                return new RejectResult(false);
            },
            //---------------------------------------------------------------------------------------------------------------
        ];

        var types = delegates.Select(it => it.Method.ReturnType).ToList();

        foreach (var currentDelegate in delegates)
        {
            await this.DispatchWithSuccess(currentDelegate);
            await this.DispatchForFailure(currentDelegate);
        }

    }

    private async Task DispatchWithSuccess(Delegate delegateToHandle)
    {
        Mock<IServiceProvider> serviceProviderMock = new Mock<IServiceProvider>();
        _ = serviceProviderMock.Setup(it => it.GetService(typeof(SampleService))).Returns(new SampleService());

        Mock<IAmqpContext> amqpContextMock = new Mock<IAmqpContext>();
        _ = amqpContextMock.Setup(it => it.MessageObject).Returns(new SampleRequest() { ReturnValue = 1 });
        _ = amqpContextMock.Setup(it => it.ServiceProvider).Returns(serviceProviderMock.Object);

        var dispatcher = new Dispatcher(delegateToHandle);

        IAMQPResult amqpResult = await dispatcher.DispatchAsync(amqpContextMock.Object);

        Type[] typesOk = [typeof(AckResult), typeof(RejectResult)];
        Type[] typesNOk = [typeof(NackResult), typeof(ReplyResult)];

        Assert.Contains(amqpResult.GetType(), typesOk);
        Assert.DoesNotContain(amqpResult.GetType(), typesNOk);
    }

    private async Task DispatchForFailure(Delegate delegateToHandle)
    {
        Mock<IServiceProvider> serviceProviderMock = new Mock<IServiceProvider>();
        _ = serviceProviderMock.Setup(it => it.GetService(typeof(SampleService))).Returns(new SampleService());

        Mock<IAmqpContext> amqpContextMock = new Mock<IAmqpContext>();
        _ = amqpContextMock.Setup(it => it.MessageObject).Returns(new SampleRequest() { ReturnValue = 1, ThrowException = true });
        _ = amqpContextMock.Setup(it => it.ServiceProvider).Returns(serviceProviderMock.Object);

        var dispatcher = new Dispatcher(delegateToHandle);

        IAMQPResult amqpResult = await dispatcher.DispatchAsync(amqpContextMock.Object);

        Type[] typesOk = [typeof(NackResult)];
        Type[] typesNOk = [typeof(AckResult), typeof(RejectResult), typeof(ReplyResult)];

        Assert.Contains(amqpResult.GetType(), typesOk);
        Assert.DoesNotContain(amqpResult.GetType(), typesNOk);
    }

    private IResultHandler GetHandler(Type type)
    {
        var amqpResultType = typeof(IAMQPResult);
        var taskOfAmqpResultType = typeof(Task<IAMQPResult>);
        var taskType = typeof(Task);

        bool isTask = type.IsAssignableTo(taskType);
        if (isTask)
        {
            if (type.IsGenericType && type.GenericTypeArguments.Length == 1)
            {
                Type taskValueType = type.GenericTypeArguments[0];
                if (taskValueType.IsAssignableTo(amqpResultType))
                {
                    return new TaskOfAmqpResultResultHandler();
                }
            }
            return new TaskResultHandler();
        }
        else if (type == typeof(void))
        {
            return new VoidResultHandler();
        }
        else
        {
            return new GenericResultHandler();
        }
    }


}
