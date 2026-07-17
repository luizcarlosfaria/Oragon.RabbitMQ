// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Provides access to the current async-local AMQP context.
/// </summary>
public interface IAmqpContextAccessor
{
    /// <summary>
    /// Gets or sets the current AMQP context for the active message scope.
    /// </summary>
    IAmqpContext Current { get; set; }
}
