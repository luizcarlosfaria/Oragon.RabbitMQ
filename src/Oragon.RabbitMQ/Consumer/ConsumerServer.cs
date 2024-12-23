// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;

namespace Oragon.RabbitMQ.Consumer;


/// <summary>
/// This class is responsible for consuming messages from RabbitMQ.
/// </summary>
public class ConsumerServer : IHostedService, IDisposable
{
    private bool disposedValue;


    /// <summary>
    /// Lock the consumers to Add Consumers
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// The consumers that will be started.
    /// </summary>
    public IEnumerable<IHostedAmqpConsumer> Consumers => [.. this.internalConsumers];
    private readonly List<IHostedAmqpConsumer> internalConsumers = [];

    /// <summary>
    /// The consumers that will be started.
    /// </summary>
    public IEnumerable<QueueConsumerBuilder> QueueConsumerBuilders => [.. this.internalQueueConsumerBuilders];
    private readonly List<QueueConsumerBuilder> internalQueueConsumerBuilders = [];


    /// <summary>
    /// Add a new consumer to the server.
    /// </summary>
    /// <param name="builder"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void AddConsumerBuilder(QueueConsumerBuilder builder)
    {
        if (this.IsReadOnly) throw new InvalidOperationException("The ConsumerServer is in readonly state");

        this.internalQueueConsumerBuilders.Add(builder);
    }


    /// <summary>
    /// This method is called when the Microsoft.Extensions.Hosting.IHostedService starts.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.IsReadOnly = true;

        foreach (QueueConsumerBuilder consumer in this.QueueConsumerBuilders)
        {
            this.internalConsumers.Add(await consumer.BuildAsync(cancellationToken).ConfigureAwait(true));
        }

        foreach (IHostedAmqpConsumer consumer in this.internalConsumers)
        {
            _ = Task.Factory.StartNew(() => consumer.StartAsync(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    /// <summary>
    /// This method is called when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IHostedAmqpConsumer consumer in this.internalConsumers.NewReverseList())
        {
            await consumer.StopAsync(cancellationToken).ConfigureAwait(true);
        }

        this.IsReadOnly = false;
    }

    /// <summary>
    /// This method is called when the application host is ready to start the service.
    /// </summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                foreach (IHostedAmqpConsumer consumer in this.internalConsumers.NewReverseList())
                {
                    consumer.DisposeAsync().AsTask().GetAwaiter().GetResult();

                    _ = this.internalConsumers.Remove(consumer);
                }

            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            this.disposedValue = true;
        }
    }

    /// <summary>
    /// This method is called when the application host is ready to start the service.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

