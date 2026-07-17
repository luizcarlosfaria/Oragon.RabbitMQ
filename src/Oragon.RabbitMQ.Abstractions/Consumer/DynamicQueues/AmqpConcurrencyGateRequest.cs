// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Application-defined gate request.
/// </summary>
public sealed record AmqpConcurrencyGateRequest(
    string Key,
    TimeSpan LeaseTime,
    IReadOnlyDictionary<string, object> Metadata);
