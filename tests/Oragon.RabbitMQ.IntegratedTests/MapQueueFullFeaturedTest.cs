// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RabbitMQ.Client;
using System.Text;
using Testcontainers.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Serialization;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotNet.Testcontainers.Builders;
using Oragon.RabbitMQ.TestsExtensions;

namespace Oragon.RabbitMQ.IntegratedTests;

public class MapQueueFullFeaturedTest : IAsyncLifetime
{
    public class ExampleMessage
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    public class ExampleService(WeakReference<EventWaitHandle> waitHandle, Action<MapQueueFullFeaturedTest.ExampleMessage> callbackToTests)
    {

        /// <summary>
        /// Only for test purposes
        /// </summary>
        public WeakReference<EventWaitHandle> WaitHandleRef { get; } = waitHandle;

        /// <summary>
        /// Only for test purposes
        /// </summary>
        public Action<ExampleMessage> CallbackToTests { get; } = callbackToTests;

        public Task TestAsync(ExampleMessage message)
        {
            Console.WriteLine($"{message.Name} : {message.Age}");

            this.CallbackToTests(message);

            this.WaitHandleRef.TryGetTarget(out EventWaitHandle? waitHandle);

            waitHandle?.Set();

            return Task.CompletedTask;
        }
    }


    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder().BuildRabbitMQ();

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

        await SafeRunner.ExecuteWithRetry<global::RabbitMQ.Client.Exceptions.BrokerUnreachableException>(async () =>
        {
            connection = await this.CreateConnectionFactory().CreateConnectionAsync();
        }).ConfigureAwait(true);

        return connection;
    }




    [Fact(Timeout = 5000)]
    public async Task MapQueueBasicSuccessTest()
    {
        const string queue = "hello";

        var originalMessage = new ExampleMessage() { Name = $"Teste - {Guid.NewGuid():D}", Age = 8 };
        ExampleMessage? receivedMessage = default;

        // Create and establish a connection.
        using var connection = await this.CreateConnectionAsync().ConfigureAwait(true);

        // Signal the completion of message reception.
        WeakReference<EventWaitHandle> waitHandleRef = new(new ManualResetEvent(false));

        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        // Singleton dependencies
        services.AddSingleton(new ActivitySource("test"));
        services.AddSingleton<IAMQPSerializer>(sp => new NewtonsoftAMQPSerializer(null));
        services.AddSingleton(connection);

        // Scoped dependencies
        services.AddScoped<ExampleService>();
        services.AddScoped<Action<ExampleMessage>>((_) => (msg) => receivedMessage = msg);
        services.AddScoped((_) => waitHandleRef);

        

        // Send a message to the channel.
        using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
        
        _ = await channel.QueueDeclareAsync(queue, false, false, false, null);

        await channel.BasicPublishAsync(string.Empty, queue, true, Encoding.Default.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(originalMessage)));
        

        var sp = services.BuildServiceProvider();

        sp.MapQueue<ExampleService, ExampleMessage>((config) =>
            config
                .WithDispatchInChildScope()
                .WithAdapter((svc, msg) => svc.TestAsync(msg))
                .WithQueueName(queue)
                .WithPrefetchCount(1)
        );

        var hostedService = sp.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

        waitHandleRef.TryGetTarget(out EventWaitHandle? waitHandle);

        _ = waitHandle.WaitOne(
            Debugger.IsAttached
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromSeconds(3)
        );

        await hostedService.StopAsync(CancellationToken.None);

        Assert.NotNull(receivedMessage);

        Assert.Equal(originalMessage.Name, receivedMessage.Name);

        Assert.Equal(originalMessage.Age, receivedMessage.Age);
    }

   

}
