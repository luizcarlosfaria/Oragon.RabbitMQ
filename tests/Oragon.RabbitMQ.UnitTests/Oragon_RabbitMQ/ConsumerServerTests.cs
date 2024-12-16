using DotNet.Testcontainers.Builders;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.TestsExtensions;
using System.Text;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class ConsumerServerTests
{
    

       [Fact]
    public async Task ConsumerServerCallStartStopAndDispose()
    {
        var innerConsumerMock = new Mock<IHostedAmqpConsumer>();
        innerConsumerMock.Setup(it => it.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
        innerConsumerMock.Setup(it => it.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
        innerConsumerMock.Setup(it => it.Validate()).Verifiable();
        innerConsumerMock.Setup(it => it.DisposeAsync()).Returns(new ValueTask()).Verifiable();

        using (var consumerServer = new ConsumerServer())
        {
            consumerServer.AddConsumer(innerConsumerMock.Object);
            await consumerServer.StartAsync(CancellationToken.None);
            await Task.Delay(50);
            await consumerServer.StopAsync(CancellationToken.None);
        }

        innerConsumerMock.Verify(it => it.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        innerConsumerMock.Verify(it => it.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        innerConsumerMock.Verify(it => it.Validate(), Times.Once);
        innerConsumerMock.Verify(it => it.DisposeAsync(), Times.Once);
    }
}
