// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Options for <see cref="RequeueToTailResult"/>.
/// </summary>
public sealed class RequeueToTailOptions
{
    /// <summary>
    /// Gets or sets the target queue. When null or whitespace, the current queue is used.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the property groups copied from the original delivery.
    /// </summary>
    public AmqpPropertyCopy CopyProperties { get; set; } = AmqpPropertyCopy.RequeueToTailDefault;

    /// <summary>
    /// Gets or sets an optional hook used to customize output properties after configured copy groups are applied.
    /// </summary>
    public Action<IReadOnlyBasicProperties, BasicProperties> ConfigureProperties { get; set; }
}
