// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;

namespace DotNetAspireApp.ApiService;

public class MessagePublisher
{

    private static readonly Queue<(ConsoleColor ForegroundColor, ConsoleColor BackgroundColor)> colorQueue = new(new[]
    {
        //(ConsoleColor.Black, ConsoleColor.White),
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

    private const bool configureAwait = true;

    private static readonly object colorQueueLock = new();

    private static int s_sequence;

    private readonly IConnectionFactory connectionFactory;

    private readonly IAmqpSerializer serializer;

    private volatile bool isBlocked;

    public string UId { get; } = Guid.NewGuid().ToString("D").Split('-').Last();
    public int Id { get; private set; } = (++MessagePublisher.s_sequence);

    public string ConsoleId => $"{this.Id:000} | {this.UId}";

    public readonly (ConsoleColor ForegroundColor, ConsoleColor BackgroundColor) consoleColors;

    public MessagePublisher(IConnectionFactory connectionFactory, IAmqpSerializer serializer)
    {
        this.connectionFactory = connectionFactory;
        this.serializer = serializer;

        lock (colorQueueLock)
        {
            this.consoleColors = colorQueue.Dequeue();
            colorQueue.Enqueue(this.consoleColors);
        }
    }


    #region Connection Management


    public void Log(string message)
    {
        string foregroundAnsi = GetAnsiCode(this.consoleColors.ForegroundColor);
        //string backgroundAnsi = GetAnsiCode(this.consoleColors.BackgroundColor);

        //Console.WriteLine($"\u001b[38;5;{foregroundAnsi}m {this.consoleColors.ForegroundColor} | {this.ConsoleId} {message}\u001b[0m");
        Console.WriteLine($"\u001b[38;5;{foregroundAnsi}m{this.ConsoleId} {message}\u001b[0m");
    }

    private static string GetAnsiCode(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => "0",
        ConsoleColor.Red => "196",
        ConsoleColor.Green => "34",
        ConsoleColor.Yellow => "11",
        ConsoleColor.Blue => "21",
        ConsoleColor.Magenta => "207",
        ConsoleColor.Cyan => "33",
        ConsoleColor.White => "255",
        ConsoleColor.DarkGray => "239",
        ConsoleColor.DarkRed => "161",
        ConsoleColor.DarkGreen => "28",
        ConsoleColor.DarkYellow => "148",
        ConsoleColor.DarkBlue => "56",
        ConsoleColor.DarkMagenta => "200",
        ConsoleColor.DarkCyan => "26",
        ConsoleColor.Gray => "246",
        _ => ""
    };


    private IConnection? connection;

    private async Task<IConnection> GetOrCreateConnectionAsync()
    {
        if (this.connection != null && this.connection.IsOpen) return this.connection;

        this.Log($"GetOrCreateConnectionAsync() BEGIN");

        await this.ReleaseConnectionAsync().ConfigureAwait(configureAwait);

        this.Log($"Creating new Connection... ");
        IConnection newConnection = await this.connectionFactory.CreateConnectionAsync($"ApiService - enqueue - {this.ConsoleId}", CancellationToken.None).ConfigureAwait(configureAwait);

        this.connection = newConnection;
        this.connection.ConnectionBlockedAsync += this.Connection_ConnectionBlockedAsync;
        this.connection.ConnectionUnblockedAsync += this.Connection_ConnectionUnblockedAsync;
        this.connection.ConnectionShutdownAsync += this.Connection_ConnectionShutdownAsync;

        this.Log($"GetOrCreateConnectionAsync() END");

        return newConnection;
    }

    private async Task Connection_ConnectionShutdownAsync(object sender, RabbitMQ.Client.Events.ShutdownEventArgs @event)
    {
        this.Log($"Connection Shutdown Start...");

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

        this.Log($"Connection Shutdown End!!");
    }

    private async Task ReleaseConnectionAsync()
    {
        this.Log($"Releasing Connection... ");

        if (this.connection != null)
        {
            this.Log($"Connection found, calling this.connection.CloseAsync(), removing delegates and setting this.connection to null");
            await this.connection.CloseAsync().ConfigureAwait(configureAwait);

            this.connection.ConnectionBlockedAsync -= this.Connection_ConnectionBlockedAsync;
            this.connection.ConnectionUnblockedAsync -= this.Connection_ConnectionUnblockedAsync;
            this.connection.ConnectionShutdownAsync -= this.Connection_ConnectionShutdownAsync;
            this.connection = null;
        }
        else
        {
            this.Log($"Connection is not set");
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

        this.Log($"GetOrCreateChannelAsync() BEGIN");

        await this.ReleaseChannelAsync().ConfigureAwait(configureAwait);

        IConnection connection = await this.GetOrCreateConnectionAsync().ConfigureAwait(configureAwait);

        this.Log($"Creating Channel... ");

        IChannel newChannel = await connection.CreateChannelAsync().ConfigureAwait(configureAwait);

        this.channel = newChannel;

        this.Log($"GetOrCreateChannelAsync() END");

        this.isBlocked = false;

        return newChannel;
    }
    private async Task ReleaseChannelAsync()
    {
        this.Log($"Releasing Channel... ");

        if (this.channel != null)
        {
            this.Log($"Channel found, calling this.channel.CloseAsync() and setting this.channel to null");

            await this.channel.CloseAsync().ConfigureAwait(configureAwait);
            this.channel = null;
        }
        else
        {
            this.Log($"Channel is not set");
        }
    }

    #endregion

    public async Task PublishAsync<T>(T message, string exchange, string routingKey, CancellationToken cancellationToken)
    {
        for (int retryWait = 0; this.isBlocked && retryWait < 90; retryWait++)
        {
            this.Log($"Connection is blocked. Waiting 10s to try publish... ");
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            if (!this.isBlocked)
            {
                this.Log($"Connection is not blocked anymore, continuing...");
                break;
            }
        }

        IChannel channel = await this.GetOrCreateChannelAsync().ConfigureAwait(configureAwait);

        BasicProperties properties = channel.CreateBasicProperties().EnsureHeaders().SetDurable(true);

        byte[] body = this.serializer.Serialize(basicProperties: properties, message: message);

        await channel.BasicPublishAsync(exchange, routingKey, false, properties, body, cancellationToken).ConfigureAwait(configureAwait);
    }

    public async Task DisposeAsync()
    {
        this.Log($"MessagePublisher disposing...");

        IChannel? localChannel = this.channel;
        this.channel = null;

        IConnection? localConnection = this.connection;
        this.connection = null;

        if (localChannel != null)
        {
            this.Log($"MessagePublisher disposing ... localChannel.CloseAsync() BEGIN");
            await localChannel.CloseAsync().ConfigureAwait(true);
            this.Log($"MessagePublisher disposing ... localChannel.CloseAsync() END");
        }

        if (localConnection != null)
        {
            this.Log($"MessagePublisher disposing ... localConnection.CloseAsync() BEGIN");
            await localConnection.CloseAsync().ConfigureAwait(true);
            this.Log($"MessagePublisher disposing ... localConnection.CloseAsync() END");
        }

        this.Log($"MessagePublisher disposing ... Everything is closed!");

        if (localChannel != null)
        {
            this.Log($"MessagePublisher disposing ... localChannel.DisposeAsync() BEGIN");
            await localChannel.DisposeAsync().ConfigureAwait(true);
            this.Log($"MessagePublisher disposing ... localChannel.DisposeAsync() END");
        }

        if (localConnection != null)
        {
            this.Log($"MessagePublisher disposing ... localConnection.DisposeAsync() BEGIN");
            await localConnection.DisposeAsync().ConfigureAwait(true);
            this.Log($"MessagePublisher disposing ... localConnection.DisposeAsync() END");
        }

        this.Log($"MessagePublisher Disposed!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

        GC.SuppressFinalize(this);
    }

}
