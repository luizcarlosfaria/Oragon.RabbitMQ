// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace DotNetAspireApp.ApiService;

public class MessagePublisher
{
    private static readonly ConcurrentQueue<(ConsoleColor ForegroundColor, ConsoleColor BackgroundColor)> colorQueue = new(new[]
    {
        (ConsoleColor.Black, ConsoleColor.White),
        (ConsoleColor.Blue, ConsoleColor.Yellow),
        (ConsoleColor.Cyan, ConsoleColor.Red),
        (ConsoleColor.DarkBlue, ConsoleColor.Gray),
        (ConsoleColor.DarkCyan, ConsoleColor.Magenta),
        (ConsoleColor.DarkGray, ConsoleColor.Green),
        (ConsoleColor.DarkGreen, ConsoleColor.Cyan),
        (ConsoleColor.DarkMagenta, ConsoleColor.DarkYellow),
        (ConsoleColor.DarkRed, ConsoleColor.DarkGray),
        (ConsoleColor.DarkYellow, ConsoleColor.DarkBlue),
        (ConsoleColor.Gray, ConsoleColor.DarkGreen),
        (ConsoleColor.Green, ConsoleColor.DarkCyan),
        (ConsoleColor.Magenta, ConsoleColor.DarkRed),
        (ConsoleColor.Red, ConsoleColor.DarkMagenta),
        (ConsoleColor.White, ConsoleColor.Black),
        (ConsoleColor.Yellow, ConsoleColor.DarkBlue),
        (ConsoleColor.DarkBlue, ConsoleColor.White),
        (ConsoleColor.DarkCyan, ConsoleColor.Yellow),
        (ConsoleColor.DarkGray, ConsoleColor.Red),
        (ConsoleColor.DarkGreen, ConsoleColor.Gray)
    });
    private static readonly object colorQueueLock = new();

    public static int Sequence { get; private set; }

    private readonly IConnectionFactory connectionFactory;
    private readonly IAmqpSerializer serializer;
    private volatile bool isBlocked;

    public string UId { get; } = Guid.NewGuid().ToString("D").Split('-').Last();
    public int Id { get; private set; } = (++MessagePublisher.Sequence);

    public string ConsoleId => $"{this.Id:000} | {this.UId}";

    public readonly (ConsoleColor ForegroundColor, ConsoleColor BackgroundColor) consoleColors;

    public MessagePublisher(IConnectionFactory connectionFactory, IAmqpSerializer serializer)
    {
        this.connectionFactory = connectionFactory;
        this.serializer = serializer;

        lock (colorQueueLock)
        {
            if (colorQueue.TryDequeue(out var colors))
            {
                this.consoleColors = colors;
                colorQueue.Enqueue(colors);
            }
        }

    }


    #region Connection Management


    private void Log(string message)
    {
        Console.ForegroundColor = this.consoleColors.ForegroundColor;
        Console.BackgroundColor = this.consoleColors.BackgroundColor;
        Console.WriteLine($"{this.ConsoleId} {message}");
        Console.ResetColor();
    }


    private IConnection? connection;

    private async Task<IConnection> GetOrCreateConnectionAsync()
    {
        if (this.connection != null && this.connection.IsOpen) return this.connection;

        await this.ReleaseConnectionAsync().ConfigureAwait(false);

        this.Log($"Creating Connection... ");
        IConnection newConnection = await this.connectionFactory.CreateConnectionAsync("ApiService - enqueue", CancellationToken.None).ConfigureAwait(false);

        this.connection = newConnection;
        this.connection.ConnectionBlockedAsync += this.Connection_ConnectionBlockedAsync;
        this.connection.ConnectionUnblockedAsync += this.Connection_ConnectionUnblockedAsync;
        this.connection.ConnectionShutdownAsync += this.Connection_ConnectionShutdownAsync;

        return newConnection;
    }

    private async Task Connection_ConnectionShutdownAsync(object sender, RabbitMQ.Client.Events.ShutdownEventArgs @event)
    {
        this.isBlocked = true;

        try
        {
            await this.ReleaseChannelAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.Log($"Release Channel by Connection Shutdown Cause error... {ex}");
        }
        this.channel = null;

        try
        {
            await this.ReleaseConnectionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.Log($"Release Connection by Connection Shutdown Cause error... {ex}");
        }
        this.connection = null;

        this.isBlocked = false;

    }

    private async Task ReleaseConnectionAsync()
    {
        if (this.connection != null)
        {
            this.connection.ConnectionBlockedAsync -= this.Connection_ConnectionBlockedAsync;
            this.connection.ConnectionUnblockedAsync -= this.Connection_ConnectionUnblockedAsync;
            this.connection.ConnectionShutdownAsync -= this.Connection_ConnectionShutdownAsync;
            await this.connection.CloseAsync().ConfigureAwait(false);
            this.connection = null;
        }
    }

    private Task Connection_ConnectionUnblockedAsync(object sender, RabbitMQ.Client.Events.AsyncEventArgs @event)
    {
        this.Log($"Connection Unblocked");
        this.isBlocked = false;
        return Task.CompletedTask;
    }

    private Task Connection_ConnectionBlockedAsync(object sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs @event)
    {
        this.Log($"Connection Blocked");
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

        this.Log($"Creating Channel... ");

        IChannel newChannel = await connection.CreateChannelAsync().ConfigureAwait(false);

        this.channel = newChannel;

        return newChannel;
    }
    private async Task ReleaseChannelAsync()
    {
        if (this.channel != null)
        {
            this.Log($"Releasing Channel... ");

            await this.channel.CloseAsync().ConfigureAwait(false);
            this.channel = null;
        }
    }

    #endregion

    public async Task PublishAsync<T>(T message, string exchange, string routingKey, CancellationToken cancellationToken)
    {
        for (var retryWait = 0; this.isBlocked && retryWait < 90; retryWait++)
        {
            this.Log($"Connection is blocked. Waiting 5s... ");
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
            this.Log($"Closing Channel... ");
            await localChannel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (localConnection != null)
        {
            this.Log($"Closing Connection... ");
            await localConnection.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }

        this.Log($"Everything is CLOSED!");

        if (localChannel != null)
        {
            this.Log($"Disposing Channel... ");
            await localChannel.DisposeAsync().ConfigureAwait(false);
        }

        if (localConnection != null)
        {
            this.Log($"Disposing Connection... ");
            await localConnection.DisposeAsync().ConfigureAwait(false);
        }

        this.Log($"Everything is DISPOSED!");

        GC.SuppressFinalize(this);
    }

}
