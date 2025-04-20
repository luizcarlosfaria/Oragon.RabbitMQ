// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Newtonsoft.Json;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace DotNetAspireApp.ApiService;

public class MessagePublisher
{
    private const bool configureAwait = true;

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
    private static int s_sequence;

    private readonly IEnvironment rabbitMQEnv;
    private readonly IAmqpSerializer serializer;
    private volatile bool isBlocked;

    public string UId { get; } = Guid.NewGuid().ToString("D").Split('-').Last();
    public int Id { get; private set; } = (++MessagePublisher.s_sequence);

    public string ConsoleId => $"{this.Id:000} | {this.UId}";

    public readonly (ConsoleColor ForegroundColor, ConsoleColor BackgroundColor) consoleColors;

    public MessagePublisher(IEnvironment rabbitMQEnv, IAmqpSerializer serializer)
    {
        this.rabbitMQEnv = rabbitMQEnv;
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

    private State[] validStates = new[]
    {
        State.Open,
        State.Reconnecting,
    };

    private async Task<IConnection> GetOrCreateConnectionAsync()
    {
        if (this.connection != null && this.validStates.Contains(this.connection.State)) return this.connection;

        this.Log($"GetOrCreateConnectionAsync() BEGIN");

        await this.ReleaseConnectionAsync().ConfigureAwait(configureAwait);

        this.Log($"Creating new Connection... ");
        IConnection newConnection = await this.rabbitMQEnv.CreateConnectionAsync().ConfigureAwait(configureAwait);

        this.connection = newConnection;

        this.connection.ChangeState += this.Connection_OnStateChanged;

        this.Log($"GetOrCreateConnectionAsync() END");

        return newConnection;
    }

    private void Connection_OnStateChanged(object sender, State previousState, State currentState, Error? failureCause)
    {

        if ((failureCause != null))
        {
            this.Log($"Connection state change from {previousState} to {currentState} with failure cause: {failureCause}");
        }
        else
        {
            this.Log($"Connection state change from {previousState} to {currentState}");
        }
    }


    private async Task ReleaseConnectionAsync()
    {
        this.Log($"Releasing Connection... ");

        if (this.connection != null)
        {
            this.Log($"Connection found, calling this.connection.CloseAsync(), removing delegates and setting this.connection to null");
            await this.connection.CloseAsync().ConfigureAwait(configureAwait);

            this.connection.ChangeState -= this.Connection_OnStateChanged;
            this.connection = null;
        }
        else
        {
            this.Log($"Connection is not set");
        }
    }



    #endregion

    #region Publisher Management

    private IPublisher? publisher;

    private async Task<IPublisher> GetOrCreatePublisherAsync()
    {
        if (this.publisher != null && this.validStates.Contains(this.publisher.State)) return this.publisher;

        this.Log($"GetOrCreatePublisherAsync() BEGIN");

        await this.ReleasePublisherAsync().ConfigureAwait(configureAwait);

        IConnection connection = await this.GetOrCreateConnectionAsync().ConfigureAwait(configureAwait);

        this.Log($"Creating Publisher... ");

        IPublisher newPublisher = await connection.PublisherBuilder().BuildAsync().ConfigureAwait(false);

        this.publisher = newPublisher;

        this.Log($"GetOrCreatePublisherAsync() END");

        this.isBlocked = false;

        return newPublisher;
    }
    private async Task ReleasePublisherAsync()
    {
        this.Log($"Releasing Publisher... ");

        if (this.publisher != null)
        {
            this.Log($"Publisher found, calling this.publisher.CloseAsync() and setting this.publisher to null");

            await this.publisher.CloseAsync().ConfigureAwait(configureAwait);
            this.publisher = null;
        }
        else
        {
            this.Log($"Publisher is not set");
        }
    }

    #endregion

    public async Task PublishAsync<T>(T message, string exchange, string routingKey, CancellationToken cancellationToken)
    {
        for (var retryWait = 0; this.isBlocked && retryWait < 90; retryWait++)
        {
            this.Log($"Connection is blocked. Waiting 10s to try publish... ");
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            if (!this.isBlocked)
            {
                this.Log($"Connection is not blocked anymore, continuing...");
                break;
            }
        }



        string json = JsonConvert.SerializeObject(message);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        IMessage amqpMessage = new AmqpMessage(bytes).ToAddress().Exchange(exchange).Key(routingKey).Build();

        IPublisher publisher = await this.GetOrCreatePublisherAsync().ConfigureAwait(configureAwait);

        PublishResult result = await publisher.PublishAsync(message: amqpMessage, cancellationToken: cancellationToken).ConfigureAwait(configureAwait);

        switch (result.Outcome.State)
        {
            case OutcomeState.Accepted:
                this.Log($"Message published successfully");
                break;
            case OutcomeState.Rejected:
                this.Log($"Message rejected");
                break;
            case OutcomeState.Released:
                this.Log($"Message released");
                break;
            default:
                this.Log($"Message outcome is {result.Outcome.State}");
                break;
        }

        this.Log($"MessagePublisher {result.Outcome.State}");


    }

    public async Task DisposeAsync()
    {
        this.Log($"MessagePublisher disposing...");

        IPublisher? localPublisher = this.publisher;
        this.publisher = null;

        IConnection? localConnection = this.connection;
        this.connection = null;

        if (localPublisher != null)
        {
            this.Log($"MessagePublisher disposing ... localPublisher.CloseAsync() BEGIN");
            await localPublisher.CloseAsync().ConfigureAwait(false);
            this.Log($"MessagePublisher disposing ... localPublisher.CloseAsync() END");
        }

        if (localConnection != null)
        {
            this.Log($"MessagePublisher disposing ... localConnection.CloseAsync() BEGIN");
            await localConnection.CloseAsync().ConfigureAwait(false);
            this.Log($"MessagePublisher disposing ... localConnection.CloseAsync() END");
        }

        this.Log($"MessagePublisher disposing ... Everything is closed!");

        if (localPublisher != null)
        {
            this.Log($"MessagePublisher disposing ... localPublisher.DisposeAsync() BEGIN");
            localPublisher.Dispose();
            this.Log($"MessagePublisher disposing ... localPublisher.DisposeAsync() END");
        }

        if (localConnection != null)
        {
            this.Log($"MessagePublisher disposing ... localConnection.DisposeAsync() BEGIN");
            localConnection.Dispose();
            this.Log($"MessagePublisher disposing ... localConnection.DisposeAsync() END");
        }

        this.Log($"MessagePublisher Disposed!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

        GC.SuppressFinalize(this);
    }

}
