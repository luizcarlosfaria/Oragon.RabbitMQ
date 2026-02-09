using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class ConsumerDescriptorTests
{
    public class TestService
    {
        public Task HandleAsync(TestMessage msg) => Task.CompletedTask;
    }

    public class TestMessage
    {
        public string Value { get; set; }
    }

    private static ConsumerDescriptor CreateDescriptor(
        IServiceProvider sp = null,
        string queueName = "test-queue",
        Delegate handler = null)
    {
        sp ??= Mock.Of<IServiceProvider>();
        handler ??= ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg);
        return new ConsumerDescriptor(sp, queueName, handler);
    }

    #region Constructor & Default Values

    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var descriptor = CreateDescriptor();

        // Assert
        Assert.Equal("test-queue", descriptor.QueueName);
        Assert.Equal(1, descriptor.PrefetchCount);
        Assert.True(descriptor.ConsumerDispatchConcurrency >= 1);
        Assert.NotNull(descriptor.ConnectionFactory);
        Assert.NotNull(descriptor.SerializerFactory);
        Assert.NotNull(descriptor.ChannelFactory);
        Assert.NotNull(descriptor.ResultForSerializationFailure);
        Assert.NotNull(descriptor.ResultForProcessFailure);
        Assert.Null(descriptor.ConsumerTag);
        Assert.False(descriptor.Exclusive);
        Assert.Null(descriptor.TopologyInitializer);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "<Pending>")]
    public void Constructor_DefaultSerializationFailure_ShouldReturnReject()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        var contextMock = new Mock<IAmqpContext>();

        // Act
        IAmqpResult result = descriptor.ResultForSerializationFailure(contextMock.Object, new Exception("test"));

        // Assert
        _ = Assert.IsType<RejectResult>(result);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "<Pending>")]
    public void Constructor_DefaultProcessFailure_ShouldReturnNack()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        var contextMock = new Mock<IAmqpContext>();

        // Act
        IAmqpResult result = descriptor.ResultForProcessFailure(contextMock.Object, new Exception("test"));

        // Assert
        _ = Assert.IsType<NackResult>(result);
    }

    #endregion

    #region WithPrefetch

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)10)]
    [InlineData((ushort)100)]
    [InlineData(ushort.MaxValue)]
    public void WithPrefetch_ValidValues_ShouldSetPrefetchCount(ushort prefetchCount)
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithPrefetch(prefetchCount);

        // Assert
        Assert.Equal(prefetchCount, descriptor.PrefetchCount);
    }

    [Fact]
    public void WithPrefetch_Zero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => descriptor.WithPrefetch(0));
    }

    #endregion

    #region WithDispatchConcurrency

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)4)]
    [InlineData((ushort)16)]
    public void WithDispatchConcurrency_ValidValues_ShouldSetConcurrency(ushort concurrency)
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithDispatchConcurrency(concurrency);

        // Assert
        Assert.Equal(concurrency, descriptor.ConsumerDispatchConcurrency);
    }

    [Fact]
    public void WithDispatchConcurrency_Zero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => descriptor.WithDispatchConcurrency(0));
    }

    #endregion

    #region WithConsumerTag

    [Fact]
    public void WithConsumerTag_ValidTag_ShouldSetConsumerTag()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithConsumerTag("my-consumer-tag");

        // Assert
        Assert.Equal("my-consumer-tag", descriptor.ConsumerTag);
    }

    [Fact]
    public void WithConsumerTag_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithConsumerTag(null));
    }

    [Fact]
    public void WithConsumerTag_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentException for empty strings
        _ = Assert.ThrowsAny<ArgumentException>(() => descriptor.WithConsumerTag(""));
    }

    [Fact]
    public void WithConsumerTag_Whitespace_ShouldThrowArgumentException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentException for whitespace strings
        _ = Assert.ThrowsAny<ArgumentException>(() => descriptor.WithConsumerTag("   "));
    }

    #endregion

    #region WithExclusive

    [Fact]
    public void WithExclusive_Default_ShouldSetExclusiveTrue()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithExclusive();

        // Assert
        Assert.True(descriptor.Exclusive);
    }

    [Fact]
    public void WithExclusive_False_ShouldSetExclusiveFalse()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithExclusive(false);

        // Assert
        Assert.False(descriptor.Exclusive);
    }

    #endregion

    #region WithConnection

    [Fact]
    public void WithConnection_ValidFactory_ShouldSetConnectionFactory()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        Func<IServiceProvider, CancellationToken, Task<IConnection>> factory = (sp, ct) => Task.FromResult(Mock.Of<IConnection>());

        // Act
        _ = descriptor.WithConnection(factory);

        // Assert
        Assert.Equal(factory, descriptor.ConnectionFactory);
    }

    [Fact]
    public void WithConnection_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithConnection(null));
    }

    #endregion

    #region WithSerializer

    [Fact]
    public void WithSerializer_ValidFactory_ShouldSetSerializerFactory()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        Func<IServiceProvider, IAmqpSerializer> factory = (sp) => Mock.Of<IAmqpSerializer>();

        // Act
        _ = descriptor.WithSerializer(factory);

        // Assert
        Assert.Equal(factory, descriptor.SerializerFactory);
    }

    [Fact]
    public void WithSerializer_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithSerializer(null));
    }

    #endregion

    #region WithChannel

    [Fact]
    public void WithChannel_ValidFactory_ShouldSetChannelFactory()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        Func<IConnection, CancellationToken, Task<IChannel>> factory = (conn, ct) => Task.FromResult(Mock.Of<IChannel>());

        // Act
        _ = descriptor.WithChannel(factory);

        // Assert
        Assert.Equal(factory, descriptor.ChannelFactory);
    }

    [Fact]
    public void WithChannel_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithChannel(null));
    }

    #endregion

    #region WithTopology

    [Fact]
    public void WithTopology_ValidInitializer_ShouldSetTopologyInitializer()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        Func<IChannel, CancellationToken, Task> initializer = (ch, ct) => Task.CompletedTask;

        // Act
        _ = descriptor.WithTopology(initializer);

        // Assert
        Assert.Equal(initializer, descriptor.TopologyInitializer);
    }

    [Fact]
    public void WithTopology_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithTopology(null));
    }

    #endregion

    #region WhenSerializationFail

    [Fact]
    public void WhenSerializationFail_ValidHandler_ShouldSetResultForSerializationFailure()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        Func<IAmqpContext, Exception, IAmqpResult> handler = (ctx, ex) => AmqpResults.Nack(true);

        // Act
        _ = descriptor.WhenSerializationFail(handler);

        // Assert
        Assert.Equal(handler, descriptor.ResultForSerializationFailure);
    }

    [Fact]
    public void WhenSerializationFail_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WhenSerializationFail(null));
    }

    #endregion

    #region WhenProcessFail

    [Fact]
    public void WhenProcessFail_ValidHandler_ShouldSetResultForProcessFailure()
    {
        // Arrange
        var descriptor = CreateDescriptor();
        Func<IAmqpContext, Exception, IAmqpResult> handler = (ctx, ex) => AmqpResults.Reject(true);

        // Act
        _ = descriptor.WhenProcessFail(handler);

        // Assert
        Assert.Equal(handler, descriptor.ResultForProcessFailure);
    }

    [Fact]
    public void WhenProcessFail_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WhenProcessFail(null));
    }

    #endregion

    #region Fluent API chaining

    [Fact]
    public void FluentApi_ShouldReturnSameDescriptorInstance()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert - all fluent methods should return the descriptor for chaining
        var result = descriptor
            .WithPrefetch(10)
            .WithDispatchConcurrency(4)
            .WithExclusive(true)
            .WithConsumerTag("my-tag")
            .WithConnection((sp, ct) => Task.FromResult(Mock.Of<IConnection>()))
            .WithSerializer((sp) => Mock.Of<IAmqpSerializer>())
            .WithChannel((conn, ct) => Task.FromResult(Mock.Of<IChannel>()))
            .WithTopology((ch, ct) => Task.CompletedTask)
            .WhenSerializationFail((ctx, ex) => AmqpResults.Reject(false))
            .WhenProcessFail((ctx, ex) => AmqpResults.Nack(false));

        Assert.NotNull(result);
    }

    #endregion

    #region Validate

    [Fact]
    public void Validate_WithValidDefaults_ShouldNotThrow()
    {
        // Arrange
        var descriptor = CreateDescriptor();

        // Act & Assert - should not throw
        descriptor.Validate();
    }

    #endregion

    #region Locking mechanism

    [Fact]
    public async Task BuildConsumerAsync_ShouldLockDescriptor()
    {
        // Arrange
        string queueName = "test-queue";

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var channelMock = new Mock<IChannel>();
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        var descriptor = new ConsumerDescriptor(sp, queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        // Act
        _ = await descriptor.BuildConsumerAsync(CancellationToken.None);

        // Assert - should be locked after build
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithPrefetch(5));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithDispatchConcurrency(2));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithConsumerTag("new-tag"));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithExclusive(true));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithConnection((sp, ct) => Task.FromResult(Mock.Of<IConnection>())));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithSerializer((sp) => Mock.Of<IAmqpSerializer>()));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithChannel((conn, ct) => Task.FromResult(Mock.Of<IChannel>())));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithTopology((ch, ct) => Task.CompletedTask));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WhenSerializationFail((ctx, ex) => AmqpResults.Reject(false)));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WhenProcessFail((ctx, ex) => AmqpResults.Nack(false)));
    }

    #endregion

    #region BuildConsumerAsync

    [Fact]
    public async Task BuildConsumerAsync_ShouldReturnInitializedConsumer()
    {
        // Arrange
        string queueName = "test-queue";

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();

        var channelMock = new Mock<IChannel>();
        var channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var sp = services.BuildServiceProvider();

        var descriptor = new ConsumerDescriptor(sp, queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        // Act
        var consumer = await descriptor.BuildConsumerAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(consumer);
        var queueConsumer = Assert.IsType<QueueConsumer>(consumer);
        Assert.True(queueConsumer.IsInitialized);
        Assert.False(queueConsumer.WasStarted);
        Assert.False(queueConsumer.IsConsuming);
    }

    #endregion
}
