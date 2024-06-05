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
using System.Threading;

namespace Oragon.RabbitMQ.IntegratedTests;

public class MultipleConsumersTest : IAsyncLifetime
{
    public class ExampleMessage
    {
        public string? Name { get; set; }
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


    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder().Build();

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
            Uri = new Uri(this._rabbitMqContainer.GetConnectionString()),
            DispatchConsumersAsync = true
        };
    }

    private Task<IConnection> CreateConnectionAsync()
    {
        return this.CreateConnectionFactory().CreateConnectionAsync();
    }

    public class Pack(string queueName, ExampleMessage originalMessage, Action<ExampleMessage> actionOnRun)
    {
        public string QueueName { get; } = queueName;
        public ExampleMessage MessageToSend { get; } = originalMessage;
        public Action<ExampleMessage> CallBack { get; } = actionOnRun;

        public ExampleMessage? MessagReceived { get; set; }

        // Signal the completion of message reception.
        public EventWaitHandle WaitHandle { get; } = new ManualResetEvent(false);
    }


    [Fact]
    public async Task MultipleQueuesTest()
    {
        Pack? pack1 = null;
        pack1 = new Pack(
            "queue1",
            new ExampleMessage() { Name = $"Teste - {Guid.NewGuid():D}", Age = 3 },
            (msg) => pack1!.MessagReceived = msg
            );

        Pack? pack2 = null;
        pack2 = new Pack(
           "queue2",
           new ExampleMessage() { Name = $"Teste - {Guid.NewGuid():D}", Age = 2 },
           (msg) => pack2!.MessagReceived = msg
           );

        List<Pack> packs = [pack1, pack2];

        // Create and establish a connection.
        using var connection = await this.CreateConnectionAsync().ConfigureAwait(true);

        ServiceCollection services = new();
        services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        // Singleton dependencies
        services.AddSingleton(new ActivitySource("test"));
        services.AddSingleton<IAMQPSerializer, NewtonsoftAMQPSerializer>();
        services.AddSingleton(connection);

        // Send a message to the channel.


        foreach (var pack in packs)
        {
            using var channel = await connection.CreateChannelAsync();

            services.AddKeyedScoped(pack.QueueName, (sp, key) => new ExampleService(pack.WaitHandle, pack.CallBack));

            services.MapQueue<ExampleService, ExampleMessage>((config) =>
                config
                    .WithDispatchInChildScope()
                    .WithKeyedService(pack.QueueName)
                    .WithAdapter((svc, msg) => svc.TestAsync(msg))
                    .WithQueueName(pack.QueueName)
                    .WithPrefetchCount(1)
            );

            _ = await channel.QueueDeclareAsync(pack.QueueName, false, false, false, null);

            await channel.ConfirmSelectAsync();

            await channel.BasicPublishAsync(string.Empty, pack.QueueName, Encoding.Default.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(pack.MessageToSend)), true);

            await channel.WaitForConfirmsOrDieAsync();
        }


        var sp = services.BuildServiceProvider();

        var hostedServices = sp.GetServices<IHostedService>();

        Assert.Equal(packs.Count, hostedServices.Count());

        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        await Task.WhenAll(
                packs.
                    Select(p => Task.Run(() =>
                        p.WaitHandle.WaitOne(
                            Debugger.IsAttached
                            ? TimeSpan.FromMinutes(5)
                            : TimeSpan.FromSeconds(3)
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
