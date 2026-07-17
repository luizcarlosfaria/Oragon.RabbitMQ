// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Consumes a queue selected at runtime for a controlled processing window.
/// </summary>
public interface IAmqpDynamicQueueConsumer
{
    /// <summary>
    /// Consumes messages from the requested queue until one stop rule is reached.
    /// </summary>
    /// <typeparam name="T">Message body type.</typeparam>
    /// <param name="request">Dynamic queue consumption request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consumption result.</returns>
    Task<DynamicQueueConsumeResult> ConsumeAsync<T>(
        DynamicQueueConsumeRequest<T> request,
        CancellationToken cancellationToken);
}
