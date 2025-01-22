// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RabbitMQ.Client;
using System.Text;
using Testcontainers.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.TestsExtensions;
using Oragon.RabbitMQ.Consumer;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;

namespace Oragon.RabbitMQ.IntegratedTests;

public class MultipleConsumersTest : IAsyncLifetime
{
    public class ExampleMessage
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class ExampleService(EventWaitHandle waitHandle, Action<ExampleMessage> callbackToTests)
    {

        /// <summary>
        /// Only for test purposes
        /// </summary>
        public EventWaitHandle WaitHandle { get; } = waitHandle;

        /// <summary>
        /// Only for test purposes
        /// </summary>
        public Action<ExampleMessage> CallbackToTests { get; } = callbackToTests;

        public Task TestAsync(ExampleMessage message)
        {
            Console.WriteLine($"{message.Name} : {message.Age}");

            this.CallbackToTests(message);

            this.WaitHandle.Set();

            return Task.CompletedTask;
        }
    }


    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder().BuildRabbitMQ();

    public async Task InitializeAsync()
    {
        await this._rabbitMqContainer.StartAsync().ConfigureAwait(true);
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
            connection = await this.CreateConnectionFactory().CreateConnectionAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);

        return connection;
    }

    public class Pack(string queueName, ExampleMessage messageToSend, Action<ExampleMessage> actionOnRun, Delegate handler)
    {
        public string QueueName { get; } = queueName;
        public ExampleMessage MessageToSend { get; } = messageToSend;
        public Action<ExampleMessage> CallBack { get; } = actionOnRun;

        public Delegate Handler { get; } = handler;

        public ExampleMessage MessagReceived { get; set; }

        // Signal the completion of message reception.
        public EventWaitHandle WaitHandle { get; } = new ManualResetEvent(false);
    }


    [Fact]
    public async Task MultipleQueuesTest()
    {
        Pack pack1 = null;
        pack1 = new Pack(
            queueName: "queue1",
            messageToSend: new ExampleMessage() { Name = $"Teste - {Guid.NewGuid():D}", Age = 3 },
            actionOnRun: (msg) => pack1!.MessagReceived = msg,
            handler: ([FromServices("queue1")] ExampleService svc, ExampleMessage msg) => svc.TestAsync(msg)
            );

        Pack pack2 = null;
        pack2 = new Pack(
           queueName: "queue2",
           messageToSend: new ExampleMessage() { Name = $"Teste - {Guid.NewGuid():D}", Age = 2 },
           actionOnRun: (msg) => pack2!.MessagReceived = msg,
           handler: ([FromServices("queue2")] ExampleService svc, ExampleMessage msg) => svc.TestAsync(msg)
           );

        List<Pack> packs = [pack1, pack2];



        ServiceCollection services = new();
        services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        services.AddRabbitMQConsumer();

        // Singleton dependencies
        services.AddSingleton(new ActivitySource("test"));
        services.AddNewtonsoftAmqpSerializer();
        services.AddSingleton(sp => this.CreateConnectionAsync().GetAwaiter().GetResult());

        // Send a message to the channel.

        foreach (var pack in packs)
        {
            services.AddKeyedScoped(pack.QueueName, (sp, key) => new ExampleService(pack.WaitHandle, pack.CallBack));
        }

        var sp = services.BuildServiceProvider();

        await sp.WaitRabbitMQAsync().ConfigureAwait(true);

        IConnection connection = sp.GetRequiredService<IConnection>();

        foreach (var pack in packs)
        {
            using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

            _ = await channel.QueueDeclareAsync(pack.QueueName, false, false, false, null);

            await channel.BasicPublishAsync(string.Empty, pack.QueueName, true, Encoding.Default.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(pack.MessageToSend)));

            sp.MapQueue(pack.QueueName, pack.Handler)
                .WithPrefetch(1);
        }

        var hostedServices = sp.GetServices<IHostedService>();

        Assert.Single(hostedServices);

        Assert.IsType<ConsumerServer>(hostedServices.Single());

        ConsumerServer consumerServer = (ConsumerServer)hostedServices.Single();

        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        Assert.Equal(packs.Count, consumerServer.Consumers.Count());

        await Task.WhenAll(
                packs.
                    Select(p => Task.Run(() =>
                        p.WaitHandle.WaitOne(
                            Debugger.IsAttached
                            ? TimeSpan.FromMinutes(5)
                            : TimeSpan.FromSeconds(5)
                        )
                    )
                )
                .ToArray()
            );



        foreach (var hostedService in hostedServices.Reverse())
        {
            await hostedService.StopAsync(CancellationToken.None);
        }


        foreach (var pack in packs)
        {
            Assert.NotNull(pack.MessagReceived);

            Assert.Equal(pack.MessageToSend.Name, pack.MessagReceived.Name);

            Assert.Equal(pack.MessageToSend.Age, pack.MessagReceived.Age);

        }


    }
}
