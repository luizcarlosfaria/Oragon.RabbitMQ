// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Concurrency lease.
/// </summary>
public interface IAmqpConcurrencyLease : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the lease was acquired.
    /// </summary>
    bool Acquired { get; }

    /// <summary>
    /// Gets the application-defined key.
    /// </summary>
    string Key { get; }
}
