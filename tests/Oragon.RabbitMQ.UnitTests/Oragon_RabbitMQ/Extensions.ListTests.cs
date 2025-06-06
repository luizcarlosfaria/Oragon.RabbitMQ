using Oragon.RabbitMQ;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class Extensions_List_Tests
{
    [Fact]
    public void NewReverseList_ShouldReverseItems()
    {
        // Arrange
        var original = new List<int> { 1, 2, 3 };

        // Act
        var result = original.NewReverseList();

        // Assert
        Assert.Equal(new List<int> { 3, 2, 1 }, result);
        Assert.Equal(new List<int> { 1, 2, 3 }, original);
    }

    [Fact]
    public void NewReverseList_ShouldThrowWhenNull()
    {
        // Arrange
        List<int>? original = null;

        // Act & Assert
        Assert.Null(original!.NewReverseList());
    }
}
