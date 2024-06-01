// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using Testcontainers.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Oragon.RabbitMQ.Serialization;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System;

namespace Oragon.RabbitMQ.IntegratedTests;

public class FullFeaturedTest : IAsyncLifetime
{
    public class ExampleMessage
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    public class ExampleService
    {
        public ExampleService(EventWaitHandle waitHandle, Action<ExampleMessage> callbackToTests)
        {
            this.WaitHandle = waitHandle;
            this.CallbackToTests = callbackToTests;
        }

        /// <summary>
        /// Only for test purposes
        /// </summary>
        public EventWaitHandle WaitHandle { get; }

        /// <summary>
        /// Only for test purposes
        /// </summary>
        public Action<ExampleMessage> CallbackToTests { get; }

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




    [Fact]
    public async Task ConsumeUsingMinimalApi()
    {
        const string queue = "hello";

        ExampleMessage originalMessage = new ExampleMessage() { Name = $"Teste - {Guid.NewGuid().ToString("D")}", Age = 8 };
        ExampleMessage receivedMessage = default;

        // Create and establish a connection.
        using var connection = await this.CreateConnectionAsync().ConfigureAwait(true);

        // Signal the completion of message reception.
        EventWaitHandle waitHandle = new ManualResetEvent(false);

        ServiceCollection services = new();
        services.AddLogging(loggingBuilder =>
           {
               loggingBuilder.AddConsole();
           });

        // Singleton dependencies
        services.AddSingleton(new ActivitySource("test"));
        services.AddSingleton<IAMQPSerializer, NewtonsoftAMQPSerializer>();
        services.AddSingleton(connection);

        // Scoped dependencies
        services.AddScoped<ExampleService>();
        services.AddScoped<Action<ExampleMessage>>((_) => (msg) => receivedMessage = msg);
        services.AddScoped((_) => waitHandle);

        services.MapQueue<ExampleService, ExampleMessage>((config) =>
            config
                .WithDispatchInChildScope()
                .WithAdapter((svc, msg) => svc.TestAsync(msg))
                .WithQueueName(queue)
                .WithPrefetchCount(1)
        );

        // Send a message to the channel.
        using var channel = await connection.CreateChannelAsync();
        await channel.ConfirmSelectAsync();
        _ = await channel.QueueDeclareAsync(queue, false, false, false, null);
        await channel.BasicPublishAsync(string.Empty, queue, Encoding.Default.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(originalMessage)), true);
        await channel.WaitForConfirmsOrDieAsync();

        var sp = services.BuildServiceProvider();

        IHostedService hostedService = sp.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

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
