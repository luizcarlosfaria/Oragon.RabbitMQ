// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using Testcontainers.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.TestsExtensions;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;

namespace Oragon.RabbitMQ.IntegratedTests;

public class MapRpcQueueFullFeaturedTest : IAsyncLifetime
{
    public class RequestMessage
    {
        public int Num1 { get; set; }
        public int Num2 { get; set; }
    }

    public class ResponseMessage
    {
        public int Result { get; set; }
    }

    public class ExampleRpcService
    {
        public ExampleRpcService()
        {

        }

        public Task<ResponseMessage> TestRpcAsync(RequestMessage requestMessage)
        {
            var returnValue = requestMessage.Num1 + requestMessage.Num2;

            var returnMessage = new ResponseMessage() { Result = returnValue };

            return Task.FromResult(returnMessage);
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
            connection = await this.CreateConnectionFactory().CreateConnectionAsync().ConfigureAwait(false);
        }).ConfigureAwait(true);

        ArgumentNullException.ThrowIfNull(connection, nameof(connection));

        return connection;
    }




    [Fact]
    public async Task MapRpcQueueTest()
    {
        const string serverQueue = "rpc-server-example";

        var originalMessage = new RequestMessage()
        {
            Num1 = Random.Shared.Next(10),
            Num2 = Random.Shared.Next(10)
        };
        ResponseMessage receivedMessage = default;

        // Create and establish a connection.
        using var connection = await this.CreateConnectionAsync().ConfigureAwait(true);


        ServiceCollection services = new();
        services.AddRabbitMQConsumer();
        services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        // Singleton dependencies
        services.AddSingleton(new ActivitySource("test"));
        services.AddNewtonsoftAmqpSerializer();
        services.AddSingleton(connection);

        // Scoped dependencies
        services.AddScoped<ExampleRpcService>();





        using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

        _ = await channel.QueueDeclareAsync(serverQueue, false, false, false, null);
        var replyQueue = await channel.QueueDeclareAsync(queue: string.Empty, exclusive: true, autoDelete: true);

        var basicProperties = channel.CreateBasicProperties()
            .SetReplyTo(replyQueue.QueueName)
            .SetMessageId(Guid.NewGuid().ToString("D"));

        await channel.BasicPublishAsync(string.Empty, serverQueue, true, basicProperties, Encoding.Default.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(originalMessage)));

        var sp = services.BuildServiceProvider();

        sp.MapQueue(serverQueue, async ([FromServices] ExampleRpcService svc, RequestMessage msg) =>
        {
            ResponseMessage reply = await svc.TestRpcAsync(msg).ConfigureAwait(true);

            return AmqpResults.Compose(AmqpResults.Ack(), AmqpResults.Reply(reply));
        }).WithPrefetch(1);


        var hostedService = sp.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        // Signal the completion of message reception.
        EventWaitHandle waitHandle = new ManualResetEvent(false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, eventArgs) =>
        {
            receivedMessage = Newtonsoft.Json.JsonConvert.DeserializeObject<ResponseMessage>(Encoding.Default.GetString(eventArgs.Body.ToArray()));

            _ = waitHandle.Set();

            return Task.CompletedTask;
        };

        var consumerTag = await channel.BasicConsumeAsync(replyQueue.QueueName, true, consumer);

        _ = waitHandle.WaitOne(
           Debugger.IsAttached
           ? TimeSpan.FromMinutes(5)
           : TimeSpan.FromSeconds(5)
       );

        await channel.BasicCancelAsync(consumerTag);

        await hostedService.StopAsync(CancellationToken.None);

        Assert.NotNull(receivedMessage);

        Assert.Equal(originalMessage.Num1 + originalMessage.Num2, receivedMessage.Result);
    }
}
