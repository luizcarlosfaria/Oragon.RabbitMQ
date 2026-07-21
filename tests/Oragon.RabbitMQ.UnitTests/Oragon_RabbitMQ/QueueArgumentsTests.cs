using Oragon.RabbitMQ;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class QueueArgumentsTests
{
    [Fact]
    public void QueueArguments_ShouldComposeCommonRabbitMqArguments()
    {
        // Act
        QueueArguments arguments = QueueArguments
            .Quorum()
            .WithSingleActiveConsumer()
            .WithDeadLetter("dead-letter-exchange", "dead-letter-routing-key")
            .WithMaxPriority(10);

        // Assert
        Assert.Equal(QueueArguments.QuorumQueueType, arguments[QueueArguments.QueueTypeKey]);
        Assert.Equal(true, arguments[QueueArguments.SingleActiveConsumerKey]);
        Assert.Equal("dead-letter-exchange", arguments[QueueArguments.DeadLetterExchangeKey]);
        Assert.Equal("dead-letter-routing-key", arguments[QueueArguments.DeadLetterRoutingKeyKey]);
        Assert.Equal((byte)10, arguments[QueueArguments.MaxPriorityKey]);
    }

    [Fact]
    public void QueueArgumentDiagnostics_ShouldReportMissingAndDifferentArguments()
    {
        // Arrange
        QueueArguments expected = QueueArguments
            .Quorum()
            .WithSingleActiveConsumer()
            .WithMaxPriority(10);

        var actual = new Dictionary<string, object>
        {
            [QueueArguments.QueueTypeKey] = "classic",
            [QueueArguments.MaxPriorityKey] = 10,
        };

        // Act
        IReadOnlyList<QueueArgumentDifference> differences = QueueArgumentDiagnostics.Compare(expected, actual);

        // Assert
        QueueArgumentDifference typeDifference = Assert.Single(
            differences,
            difference => difference.Key == QueueArguments.QueueTypeKey);
        Assert.Equal(QueueArgumentDifferenceType.Different, typeDifference.Type);

        QueueArgumentDifference missingSac = Assert.Single(
            differences,
            difference => difference.Key == QueueArguments.SingleActiveConsumerKey);
        Assert.Equal(QueueArgumentDifferenceType.Missing, missingSac.Type);

        Assert.DoesNotContain(differences, difference => difference.Key == QueueArguments.MaxPriorityKey);
    }

    [Fact]
    public void QueueArgumentDiagnostics_WhenBothValuesAreEquivalentByteArrays_ShouldNotReportDifference()
    {
        // Arrange
        var expected = new Dictionary<string, object>
        {
            [QueueArguments.DeadLetterExchangeKey] = "dead-letter-exchange"u8.ToArray(),
        };

        var actual = new Dictionary<string, object>
        {
            [QueueArguments.DeadLetterExchangeKey] = "dead-letter-exchange"u8.ToArray(),
        };

        // Act
        IReadOnlyList<QueueArgumentDifference> differences = QueueArgumentDiagnostics.Compare(expected, actual);

        // Assert
        Assert.Empty(differences);
    }

    [Fact]
    public void QueueArgumentDiagnostics_WhenByteArraysHaveDifferentContent_ShouldReportDifference()
    {
        // Arrange
        var expected = new Dictionary<string, object>
        {
            [QueueArguments.DeadLetterExchangeKey] = "dead-letter-exchange"u8.ToArray(),
        };

        var actual = new Dictionary<string, object>
        {
            [QueueArguments.DeadLetterExchangeKey] = "another-exchange"u8.ToArray(),
        };

        // Act
        IReadOnlyList<QueueArgumentDifference> differences = QueueArgumentDiagnostics.Compare(expected, actual);

        // Assert
        QueueArgumentDifference difference = Assert.Single(differences);
        Assert.Equal(QueueArguments.DeadLetterExchangeKey, difference.Key);
        Assert.Equal(QueueArgumentDifferenceType.Different, difference.Type);
    }
}
