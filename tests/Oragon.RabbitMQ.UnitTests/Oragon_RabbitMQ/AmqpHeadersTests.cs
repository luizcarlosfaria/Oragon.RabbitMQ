using System.Text;
using Moq;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class AmqpHeadersTests
{
    [Fact]
    public void Get_ShouldConvertCommonHeaderTypes()
    {
        // Arrange
        var propertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = propertiesMock.SetupGet(it => it.Headers).Returns(new Dictionary<string, object>
        {
            ["text"] = Encoding.UTF8.GetBytes("hello"),
            ["count"] = "12",
            ["enabled"] = "true",
            ["x-delivery-count"] = 5,
        });
        _ = propertiesMock.SetupGet(it => it.Priority).Returns((byte)8);

        // Act
        string text = AmqpHeaders.Get<string>(propertiesMock.Object, "text");
        int count = AmqpHeaders.Get<int>(propertiesMock.Object, "count");
        bool enabled = AmqpHeaders.Get<bool>(propertiesMock.Object, "enabled");
        long? deliveryCount = AmqpHeaders.GetDeliveryCount(propertiesMock.Object);
        byte priority = AmqpHeaders.GetPriority(propertiesMock.Object);

        // Assert
        Assert.Equal("hello", text);
        Assert.Equal(12, count);
        Assert.True(enabled);
        Assert.Equal(5L, deliveryCount);
        Assert.Equal((byte)8, priority);
    }

    [Fact]
    public void Get_WhenHeaderIsMissingAndTypeIsNullable_ShouldReturnNull()
    {
        // Arrange
        var propertiesMock = new Mock<IReadOnlyBasicProperties>();
        _ = propertiesMock.SetupGet(it => it.Headers).Returns(new Dictionary<string, object>());

        // Act
        long? result = AmqpHeaders.Get<long?>(propertiesMock.Object, "missing");

        // Assert
        Assert.Null(result);
    }
}
