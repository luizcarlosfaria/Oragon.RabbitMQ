// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RabbitMQ.Client;
using System.Text;
using System.Globalization;
using Testcontainers.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Serialization;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.TestsExtensions;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.IntegratedTests;

public class ConventionBindingTest : IAsyncLifetime
{
    public class ExampleMessage
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder(Constants.RabbitMQContainerImage).BuildRabbitMQ();

    public Task InitializeAsync()
    {
        return this._rabbitMqContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return this._rabbitMqContainer.DisposeAsync().AsTask();
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            Uri = new Uri(this._rabbitMqContainer.GetConnectionString())
        };
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        IConnection connection = null;

        await SafeRunner.ExecuteWithRetry<global::RabbitMQ.Client.Exceptions.BrokerUnreachableException>(async () => connection = await this.CreateConnectionFactory().CreateConnectionAsync().ConfigureAwait(false)).ConfigureAwait(true);

        return connection;
    }

    private static TimeSpan WaitTimeout => Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10);


    [Fact(Timeout = 30000)]
    public async Task PriorityConventionBindingTest()
    {
        const string byteQueue = "PriorityConventionBindingTest-byte";
        const string intQueue = "PriorityConventionBindingTest-int";
        const string longQueue = "PriorityConventionBindingTest-long";
        const byte originalPriority = 5;

        var originalMessage = new ExampleMessage() { Name = $"Teste - {Guid.NewGuid():D}", Age = 8 };
        byte? receivedBytePriority = default;
        int? receivedIntPriority = default;
        long? receivedLongPriority = default;

        // Create and establish a connection.
        using var connection = await this.CreateConnectionAsync().ConfigureAwait(true);

        // Signal the completion of message reception on all queues.
        using var countdown = new CountdownEvent(3);

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        // Singleton dependencies
        services.AddSingleton(new ActivitySource("test"));
        services.AddNewtonsoftAmqpSerializer();
        services.AddSingleton(connection ?? throw new InvalidOperationException("Connection is null"));

        // Send a message with priority to each queue.
        using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        byte[] body = Encoding.Default.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(originalMessage));

        foreach (string queue in new[] { byteQueue, intQueue, longQueue })
        {
            _ = await channel.QueueDeclareAsync(queue, true, false, false, null);

            await channel.BasicPublishAsync(string.Empty, queue, true, new BasicProperties { Priority = originalPriority }, body);
        }


        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(byteQueue, (ExampleMessage msg, byte? priority) =>
            {
                receivedBytePriority = priority;
                _ = countdown.Signal();
            })
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConsumerTag(byteQueue)
            .WithConnection((sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>()))
            .WithSerializer((sp) => sp.GetRequiredService<IAmqpSerializer>());

        _ = sp.MapQueue(intQueue, (ExampleMessage msg, int? priority) =>
            {
                receivedIntPriority = priority;
                _ = countdown.Signal();
            })
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConsumerTag(intQueue)
            .WithConnection((sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>()))
            .WithSerializer((sp) => sp.GetRequiredService<IAmqpSerializer>());

        _ = sp.MapQueue(longQueue, (ExampleMessage msg, long? priority) =>
            {
                receivedLongPriority = priority;
                _ = countdown.Signal();
            })
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConsumerTag(longQueue)
            .WithConnection((sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>()))
            .WithSerializer((sp) => sp.GetRequiredService<IAmqpSerializer>());


        IHostedService hostedService = sp.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

        bool allReceived = countdown.Wait(WaitTimeout);

        await hostedService.StopAsync(CancellationToken.None);

        Assert.True(allReceived, "Not all queues delivered the message within the timeout");

        Assert.Equal(originalPriority, receivedBytePriority);
        Assert.Equal(originalPriority, receivedIntPriority);
        Assert.Equal(originalPriority, receivedLongPriority);
    }


    [Fact(Timeout = 30000)]
    public async Task DeliveryCountConventionBindingTest()
    {
        const string queue1 = "DeliveryCountConventionBindingTest-int-nullableLong";
        const string queue2 = "DeliveryCountConventionBindingTest-long-nullableInt";

        var originalMessage = new ExampleMessage() { Name = $"Teste - {Guid.NewGuid():D}", Age = 8 };

        var queue1Deliveries = new List<(long? DeliveryCount, long? Attempts)>();
        var queue2Deliveries = new List<(long? DeliveryCount, long? Attempts)>();

        // Create and establish a connection.
        using var connection = await this.CreateConnectionAsync().ConfigureAwait(true);

        // Signal the redelivery (second delivery) on each queue.
        using var queue1Done = new ManualResetEvent(false);
        using var queue2Done = new ManualResetEvent(false);

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        // Singleton dependencies
        services.AddSingleton(new ActivitySource("test"));
        services.AddNewtonsoftAmqpSerializer();
        services.AddSingleton(connection ?? throw new InvalidOperationException("Connection is null"));

        // Send a message to each quorum queue (only quorum queues emit the x-delivery-count header on redeliveries).
        using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        var quorumArguments = new Dictionary<string, object> { ["x-queue-type"] = "quorum" };
        byte[] body = Encoding.Default.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(originalMessage));

        foreach (string queue in new[] { queue1, queue2 })
        {
            _ = await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: quorumArguments);

            await channel.BasicPublishAsync(string.Empty, queue, true, body);
        }


        ServiceProvider sp = services.BuildServiceProvider();

        _ = sp.MapQueue(queue1, IAmqpResult (ExampleMessage msg, int? deliveryCount, long? attempts) =>
            {
                Console.WriteLine($"[queue1] delivery: deliveryCount={deliveryCount} attempts={attempts?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
                queue1Deliveries.Add((deliveryCount, attempts));

                //first delivery: force a counted broker redelivery (on RabbitMQ 4.x quorum queues,
                //basic.reject increments x-delivery-count while basic.nack with requeue does not)
                if (attempts == null) return AmqpResults.Reject(requeue: true);

                _ = queue1Done.Set();
                return AmqpResults.Ack();
            })
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConsumerTag(queue1)
            .WithConnection((sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>()))
            .WithSerializer((sp) => sp.GetRequiredService<IAmqpSerializer>());

        _ = sp.MapQueue(queue2, IAmqpResult (ExampleMessage msg, long? deliveryCount, int? attempts) =>
            {
                Console.WriteLine($"[queue2] delivery: deliveryCount={deliveryCount} attempts={attempts?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
                queue2Deliveries.Add((deliveryCount, attempts));

                //first delivery: force a counted broker redelivery (on RabbitMQ 4.x quorum queues,
                //basic.reject increments x-delivery-count while basic.nack with requeue does not)
                if (attempts == null) return AmqpResults.Reject(requeue: true);

                _ = queue2Done.Set();
                return AmqpResults.Ack();
            })
            .WithPrefetch(1)
            .WithDispatchConcurrency(1)
            .WithConsumerTag(queue2)
            .WithConnection((sp, ct) => Task.FromResult(sp.GetRequiredService<IConnection>()))
            .WithSerializer((sp) => sp.GetRequiredService<IAmqpSerializer>());


        IHostedService hostedService = sp.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

        bool queue1Received = queue1Done.WaitOne(WaitTimeout);
        bool queue2Received = queue2Done.WaitOne(WaitTimeout);

        await hostedService.StopAsync(CancellationToken.None);

        Assert.True(queue1Received, "Queue1 did not receive the redelivery within the timeout");
        Assert.True(queue2Received, "Queue2 did not receive the redelivery within the timeout");

        // First delivery: header is absent -> null for nullable types.
        // Redelivery: broker sets x-delivery-count = 1.
        Assert.Equal([(null, null), (1L, 1L)], queue1Deliveries);
        Assert.Equal([(null, null), (1L, 1L)], queue2Deliveries);
    }

}
