// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using Testcontainers.RabbitMq;

namespace Oragon.RabbitMQ.IntegratedTests;

public class TestContainersTest: IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder().BuildRabbitMQ();
    public Task InitializeAsync()
    {
        return this._rabbitMqContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return this._rabbitMqContainer.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task ConsumeMessageFromQueueUsingTestContainersAsync()
    {
        const string queue = "hello";

        var message = "Hello World! " + Guid.NewGuid().ToString("D");

        string actualMessage = null;

       

        // Create and establish a connection.
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(this._rabbitMqContainer.GetConnectionString())
        };
        using var connection = await connectionFactory.CreateConnectionAsync().ConfigureAwait(true);

        // Send a message to the channel.
        using var channel = await connection.CreateChannelAsync();

        _ = await channel.QueueDeclareAsync(queue, false, false, false, null);

        await channel.BasicPublishAsync(string.Empty, queue, false, Encoding.Default.GetBytes(message));

        // Signal the completion of message reception.
        EventWaitHandle waitHandle = new ManualResetEvent(false);

        // Consume a message from the channel.
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, eventArgs) =>
        {
            actualMessage = Encoding.Default.GetString(eventArgs.Body.ToArray());
            _ = waitHandle.Set();
            return Task.CompletedTask;
        };

        _ = await channel.BasicConsumeAsync(queue, true, consumer);

        _ = waitHandle.WaitOne(TimeSpan.FromSeconds(1));

        Assert.Equal(message, actualMessage);
    }
}
