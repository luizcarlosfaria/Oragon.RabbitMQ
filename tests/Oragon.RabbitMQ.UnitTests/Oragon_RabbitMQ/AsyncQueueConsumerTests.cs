using Moq;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class AsyncQueueConsumerTests
{
    public class ServiceDemo
    { }

    public class RequestMessageDemo
    { }

    public class ResponseMessageDemo
    { }


    [Fact]
    public void CreateBasicProperties_Should_Return_New_BasicProperties()
    {
        // Arrange
        var channel = new Mock<IChannel>().Object;

        // Act
        var result = channel.CreateBasicProperties();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<BasicProperties>(result);
    }

}
