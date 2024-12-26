// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace DotNetAspireApp.ApiService;

public class MessagePublisher
{
    private readonly IConnectionFactory connectionFactory;
    private readonly IAMQPSerializer serializer;
    private volatile bool isBlocked;

    public string Id { get; } = Guid.NewGuid().ToString("D").Split('-').Last();

    public MessagePublisher(IConnectionFactory connectionFactory, IAMQPSerializer serializer)
    {
        this.connectionFactory = connectionFactory;
        this.serializer = serializer;
    }


    #region Connection Management

    private IConnection? connection;

    private async Task<IConnection> GetOrCreateConnectionAsync()
    {
        if (this.connection != null && this.connection.IsOpen) return this.connection;

        await this.ReleaseConnectionAsync().ConfigureAwait(false);

        Console.WriteLine($"{this.Id} Creating Connection... ");
        IConnection newConnection = await this.connectionFactory.CreateConnectionAsync("ApiService - enqueue", CancellationToken.None).ConfigureAwait(false);

        this.connection = newConnection;
        this.connection.ConnectionBlockedAsync += this.Connection_ConnectionBlockedAsync;
        this.connection.ConnectionUnblockedAsync += this.Connection_ConnectionUnblockedAsync;

        return newConnection;
    }
    private async Task ReleaseConnectionAsync()
    {
        if (this.connection != null)
        {
            this.connection.ConnectionBlockedAsync -= this.Connection_ConnectionBlockedAsync;
            this.connection.ConnectionUnblockedAsync -= this.Connection_ConnectionUnblockedAsync;
            await this.connection.CloseAsync().ConfigureAwait(false);
            this.connection = null;
        }
    }

    private Task Connection_ConnectionUnblockedAsync(object sender, RabbitMQ.Client.Events.AsyncEventArgs @event)
    {
        Console.WriteLine($"{this.Id} Connection Unblocked");
        this.isBlocked = false;
        return Task.CompletedTask;
    }

    private Task Connection_ConnectionBlockedAsync(object sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs @event)
    {
        Console.WriteLine($"{this.Id} Connection Blocked");
        this.isBlocked = true;
        return Task.CompletedTask;
    }

    #endregion

    #region Channel Management

    private IChannel? channel;

    private async Task<IChannel> GetOrCreateChannelAsync()
    {
        if (this.channel != null && this.channel.IsOpen) return this.channel;

        await this.ReleaseChannelAsync().ConfigureAwait(false);

        IConnection connection = await this.GetOrCreateConnectionAsync().ConfigureAwait(false);

        Console.WriteLine($"{this.Id} Creating Channel... ");

        IChannel newChannel = await connection.CreateChannelAsync().ConfigureAwait(false);

        this.channel = newChannel;

        return newChannel;
    }
    private async Task ReleaseChannelAsync()
    {
        if (this.channel != null)
        {
            Console.WriteLine($"{this.Id} Releasing Channel... ");

            await this.channel.CloseAsync().ConfigureAwait(false);
            this.channel = null;
        }
    }

    #endregion

    public async Task PublishAsync<T>(T message, string exchange, string routingKey, CancellationToken cancellationToken)
    {
        for (var retryWait = 0; this.isBlocked && retryWait < 90; retryWait++)
        {
            Console.WriteLine($"{this.Id} Connection is blocked. Waiting 5s... ");
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            if (!this.isBlocked) break;
        }

        IChannel channel = await this.GetOrCreateChannelAsync().ConfigureAwait(false);

        BasicProperties properties = channel.CreateBasicProperties().EnsureHeaders().SetDurable(true);

        var body = this.serializer.Serialize(basicProperties: properties, message: message);

        await channel.BasicPublishAsync(exchange, routingKey, false, properties, body, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        IChannel? localChannel = this.channel;
        this.channel = null;

        IConnection? localConnection = this.connection;
        this.connection = null;

        if (localChannel != null)
        {
            Console.WriteLine($"{this.Id} Closing Channel... ");
            await localChannel.CloseAsync().ConfigureAwait(false);
        }

        if (localConnection != null)
        {
            Console.WriteLine($"{this.Id} Closing Connection... ");
            await localConnection.CloseAsync().ConfigureAwait(false);
        }

        Console.WriteLine($"{this.Id} Everything is CLOSED!");

        if (localChannel != null)
        {
            Console.WriteLine($"{this.Id} Disposing Channel... ");
            await localChannel.DisposeAsync().ConfigureAwait(false);
        }

        if (localConnection != null)
        {
            Console.WriteLine($"{this.Id} Disposing Connection... ");
            await localConnection.DisposeAsync().ConfigureAwait(false);
        }

        Console.WriteLine($"{this.Id} Everything is DISPOSED!");

        GC.SuppressFinalize(this);
    }

}
