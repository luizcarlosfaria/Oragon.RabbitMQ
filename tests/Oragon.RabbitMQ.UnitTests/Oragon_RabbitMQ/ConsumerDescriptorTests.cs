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
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Assert
        Assert.Equal("test-queue", descriptor.QueueName);
        Assert.Equal(1, descriptor.PrefetchCount);
        Assert.True(descriptor.ConsumerDispatchConcurrency >= 1);
        Assert.NotNull(descriptor.ConnectionFactory);
        Assert.NotNull(descriptor.SerializerFactory);
        Assert.NotNull(descriptor.ChannelFactory);
        Assert.NotNull(descriptor.ResultForSerializationFailure);
        Assert.NotNull(descriptor.ResultForProcessFailure);
        Assert.NotNull(descriptor.ResultForResultExecutionFailure);
        Assert.Null(descriptor.ConsumerTag);
        Assert.False(descriptor.Exclusive);
        Assert.Null(descriptor.TopologyInitializer);
        Assert.Null(descriptor.GracefulShutdownOptions);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "<Pending>")]
    public void Constructor_DefaultSerializationFailure_ShouldReturnReject()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
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
        ConsumerDescriptor descriptor = CreateDescriptor();
        var contextMock = new Mock<IAmqpContext>();

        // Act
        IAmqpResult result = descriptor.ResultForProcessFailure(contextMock.Object, new Exception("test"));

        // Assert
        _ = Assert.IsType<NackResult>(result);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "<Pending>")]
    public void Constructor_DefaultResultExecutionFailure_ShouldNackWithoutRequeue()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
        var contextMock = new Mock<IAmqpContext>();

        // Act
        IAmqpResult result = descriptor.ResultForResultExecutionFailure(contextMock.Object, new Exception("test"));

        // Assert
        NackResult nackResult = Assert.IsType<NackResult>(result);
        Assert.False(nackResult.Requeue);
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
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithPrefetch(prefetchCount);

        // Assert
        Assert.Equal(prefetchCount, descriptor.PrefetchCount);
    }

    [Fact]
    public void WithPrefetch_Zero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

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
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithDispatchConcurrency(concurrency);

        // Assert
        Assert.Equal(concurrency, descriptor.ConsumerDispatchConcurrency);
    }

    [Fact]
    public void WithDispatchConcurrency_Zero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => descriptor.WithDispatchConcurrency(0));
    }

    #endregion

    #region WithConsumerTag

    [Fact]
    public void WithConsumerTag_ValidTag_ShouldSetConsumerTag()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithConsumerTag("my-consumer-tag");

        // Assert
        Assert.Equal("my-consumer-tag", descriptor.ConsumerTag);
    }

    [Fact]
    public void WithConsumerTag_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithConsumerTag(null));
    }

    [Fact]
    public void WithConsumerTag_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentException for empty strings
        _ = Assert.ThrowsAny<ArgumentException>(() => descriptor.WithConsumerTag(""));
    }

    [Fact]
    public void WithConsumerTag_Whitespace_ShouldThrowArgumentException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert - ThrowIfNullOrWhiteSpace throws ArgumentException for whitespace strings
        _ = Assert.ThrowsAny<ArgumentException>(() => descriptor.WithConsumerTag("   "));
    }

    #endregion

    #region WithExclusive

    [Fact]
    public void WithExclusive_Default_ShouldSetExclusiveTrue()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithExclusive();

        // Assert
        Assert.True(descriptor.Exclusive);
    }

    [Fact]
    public void WithExclusive_False_ShouldSetExclusiveFalse()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

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
        ConsumerDescriptor descriptor = CreateDescriptor();
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
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithConnection(null));
    }

    #endregion

    #region WithSerializer

    [Fact]
    public void WithSerializer_ValidFactory_ShouldSetSerializerFactory()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
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
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithSerializer(null));
    }

    #endregion

    #region WithChannel

    [Fact]
    public async Task WithChannel_ValidFactory_ShouldSetChannelFactory()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
        IConnection expectedConnection = Mock.Of<IConnection>();
        IChannel expectedChannel = Mock.Of<IChannel>();
        var wasCalled = false;
        Func<IConnection, CancellationToken, Task<IChannel>> factory = (conn, ct) =>
        {
            wasCalled = ReferenceEquals(expectedConnection, conn);
            return Task.FromResult(expectedChannel);
        };

        // Act
        _ = descriptor.WithChannel(factory);
        IChannel actualChannel = await descriptor.ChannelFactory(
            Mock.Of<IServiceProvider>(),
            expectedConnection,
            CancellationToken.None);

        // Assert
        Assert.True(wasCalled);
        Assert.Same(expectedChannel, actualChannel);
    }

    [Fact]
    public void WithChannel_ServiceProviderFactory_ShouldSetChannelFactory()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
        Func<IServiceProvider, IConnection, CancellationToken, Task<IChannel>> factory = (sp, conn, ct) => Task.FromResult(Mock.Of<IChannel>());

        // Act
        _ = descriptor.WithChannel(factory);

        // Assert
        Assert.Same(factory, descriptor.ChannelFactory);
    }

    [Fact]
    public void WithChannel_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithChannel((Func<IConnection, CancellationToken, Task<IChannel>>)null));
    }

    #endregion

    #region WithTopology

    [Fact]
    public async Task WithTopology_ValidInitializer_ShouldSetTopologyInitializer()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
        IChannel expectedChannel = Mock.Of<IChannel>();
        var wasCalled = false;
        Func<IChannel, CancellationToken, Task> initializer = (ch, ct) =>
        {
            wasCalled = ReferenceEquals(expectedChannel, ch);
            return Task.CompletedTask;
        };

        // Act
        _ = descriptor.WithTopology(initializer);
        await descriptor.TopologyInitializer(
            Mock.Of<IServiceProvider>(),
            expectedChannel,
            CancellationToken.None);

        // Assert
        Assert.True(wasCalled);
    }

    [Fact]
    public void WithTopology_ServiceProviderInitializer_ShouldSetTopologyInitializer()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
        Func<IServiceProvider, IChannel, CancellationToken, Task> initializer = (sp, ch, ct) => Task.CompletedTask;

        // Act
        _ = descriptor.WithTopology(initializer);

        // Assert
        Assert.Same(initializer, descriptor.TopologyInitializer);
    }

    [Fact]
    public void WithTopology_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WithTopology((Func<IChannel, CancellationToken, Task>)null));
    }

    #endregion

    #region WhenSerializationFail

    [Fact]
    public void WhenSerializationFail_ValidHandler_ShouldSetResultForSerializationFailure()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
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
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WhenSerializationFail(null));
    }

    #endregion

    #region WhenProcessFail

    [Fact]
    public void WhenProcessFail_ValidHandler_ShouldSetResultForProcessFailure()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
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
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WhenProcessFail(null));
    }

    #endregion

    #region WhenResultExecutionFail

    [Fact]
    public void WhenResultExecutionFail_ValidHandler_ShouldSetResultForResultExecutionFailure()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();
        Func<IAmqpContext, Exception, IAmqpResult> handler = (ctx, ex) => AmqpResults.Reject(false);

        // Act
        _ = descriptor.WhenResultExecutionFail(handler);

        // Assert
        Assert.Equal(handler, descriptor.ResultForResultExecutionFailure);
    }

    [Fact]
    public void WhenResultExecutionFail_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => descriptor.WhenResultExecutionFail(null));
    }

    #endregion

    #region WithGracefulShutdown

    [Fact]
    public void WithGracefulShutdown_ValidOptions_ShouldSetGracefulShutdownOptions()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act
        _ = descriptor.WithGracefulShutdown(options =>
        {
            options.CancelContextTokenOnStop = true;
            options.WaitForInFlightMessages = true;
            options.DrainTimeout = TimeSpan.FromSeconds(10);
        });

        // Assert
        Assert.NotNull(descriptor.GracefulShutdownOptions);
        Assert.True(descriptor.GracefulShutdownOptions.CancelContextTokenOnStop);
        Assert.True(descriptor.GracefulShutdownOptions.WaitForInFlightMessages);
        Assert.Equal(TimeSpan.FromSeconds(10), descriptor.GracefulShutdownOptions.DrainTimeout);
    }

    [Fact]
    public void WithGracefulShutdown_InvalidDrainTimeout_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => descriptor.WithGracefulShutdown(options => options.DrainTimeout = TimeSpan.Zero));
    }

    #endregion

    #region Fluent API chaining

    [Fact]
    public void FluentApi_ShouldReturnSameDescriptorInstance()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

        // Act & Assert - all fluent methods should return the descriptor for chaining
        IConsumerDescriptor result = descriptor
            .WithPrefetch(10)
            .WithDispatchConcurrency(4)
            .WithExclusive(true)
            .WithConsumerTag("my-tag")
            .WithConnection((sp, ct) => Task.FromResult(Mock.Of<IConnection>()))
            .WithSerializer((sp) => Mock.Of<IAmqpSerializer>())
            .WithChannel((conn, ct) => Task.FromResult(Mock.Of<IChannel>()))
            .WithTopology((ch, ct) => Task.CompletedTask)
            .WhenSerializationFail((ctx, ex) => AmqpResults.Reject(false))
            .WhenProcessFail((ctx, ex) => AmqpResults.Nack(false))
            .WhenResultExecutionFail((ctx, ex) => AmqpResults.Reject(false))
            .WithGracefulShutdown(options => options.WaitForInFlightMessages = true);

        Assert.NotNull(result);
    }

    #endregion

    #region Validate

    [Fact]
    public void Validate_WithValidDefaults_ShouldNotThrow()
    {
        // Arrange
        ConsumerDescriptor descriptor = CreateDescriptor();

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
        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        IConnection connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

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
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WhenResultExecutionFail((ctx, ex) => AmqpResults.Reject(false)));
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithGracefulShutdown(options => options.WaitForInFlightMessages = true));
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
        IChannel channel = channelMock.Object;

        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        IConnection connection = connectionMock.Object;
        _ = services.AddSingleton(connection);

        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        ServiceProvider sp = services.BuildServiceProvider();

        var descriptor = new ConsumerDescriptor(sp, queueName, ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        // Act
        IHostedAmqpConsumer consumer = await descriptor.BuildConsumerAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(consumer);
        QueueConsumer queueConsumer = Assert.IsType<QueueConsumer>(consumer);
        Assert.True(queueConsumer.IsInitialized);
        Assert.False(queueConsumer.WasStarted);
        Assert.False(queueConsumer.IsConsuming);
    }

    [Fact]
    public async Task BuildConsumerAsync_WhenInitializeFails_ShouldNotLockDescriptor()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var firstConnectionMock = new Mock<IConnection>();
        _ = firstConnectionMock
            .Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("channel factory failed"));

        _ = services.AddSingleton(firstConnectionMock.Object);

        ServiceProvider sp = services.BuildServiceProvider();

        var descriptor = new ConsumerDescriptor(
            sp,
            "test-queue",
            ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg));

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => descriptor.BuildConsumerAsync(CancellationToken.None));

        // Assert
        Assert.Equal("channel factory failed", exception.Message);
        _ = descriptor.WithPrefetch(5);
        Assert.Equal((ushort)5, descriptor.PrefetchCount);
    }

    [Fact]
    public async Task BuildConsumerAsync_AfterInitializationFailure_ShouldAllowRetryAndLockOnSuccess()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        _ = services.AddLogging();
        _ = services.AddNewtonsoftAmqpSerializer();
        _ = services.AddScoped<TestService>();

        var workingChannelMock = new Mock<IChannel>();
        var workingConnectionMock = new Mock<IConnection>();
        _ = workingConnectionMock
            .Setup(it => it.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workingChannelMock.Object);

        _ = services.AddSingleton(workingConnectionMock.Object);
        ServiceProvider sp = services.BuildServiceProvider();
        var shouldFailInitialization = true;

        IConsumerDescriptor descriptor = new ConsumerDescriptor(
            sp,
            "test-queue",
            ([FromServices] TestService svc, [FromBody] TestMessage msg) => svc.HandleAsync(msg))
            .WithTopology((_, _) =>
            {
                if (shouldFailInitialization)
                {
                    throw new InvalidOperationException("topology failed");
                }

                return Task.CompletedTask;
            });

        // Act
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => descriptor.BuildConsumerAsync(CancellationToken.None));
        _ = descriptor.WithConsumerTag("retry-consumer");
        shouldFailInitialization = false;
        IHostedAmqpConsumer consumer = await descriptor.BuildConsumerAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(consumer);
        Assert.Equal("retry-consumer", descriptor.ConsumerTag);
        _ = Assert.Throws<InvalidOperationException>(() => descriptor.WithPrefetch(10));
    }

    #endregion
}
