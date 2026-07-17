// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Gate used by applications to control dynamic consumption concurrency.
/// </summary>
public interface IAmqpConcurrencyGate
{
    /// <summary>
    /// Tries to acquire a concurrency lease.
    /// </summary>
    /// <param name="request">Gate request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A lease.</returns>
    ValueTask<IAmqpConcurrencyLease> TryAcquireAsync(
        AmqpConcurrencyGateRequest request,
        CancellationToken cancellationToken);
}
