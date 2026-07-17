// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Start hook decision type.
/// </summary>
public enum DynamicQueueStartDecisionType
{
    /// <summary>
    /// Continue consuming.
    /// </summary>
    Allow,

    /// <summary>
    /// Stop without opening a broker consumer.
    /// </summary>
    Skip,

    /// <summary>
    /// Stop and signal that consumption should be tried later.
    /// </summary>
    Defer,

    /// <summary>
    /// Stop as a fault.
    /// </summary>
    Fail,
}
