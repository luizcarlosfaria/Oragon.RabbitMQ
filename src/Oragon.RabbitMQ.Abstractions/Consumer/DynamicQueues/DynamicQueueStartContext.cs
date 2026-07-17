// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Context passed to a dynamic queue start hook.
/// </summary>
public sealed record DynamicQueueStartContext(
    string QueueName,
    long? InitialReadyCount,
    IServiceProvider Services,
    IReadOnlyDictionary<string, object> Metadata);
