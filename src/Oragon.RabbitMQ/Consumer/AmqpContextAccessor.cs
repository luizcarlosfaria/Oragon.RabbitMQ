// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Async-local AMQP context accessor.
/// </summary>
public sealed class AmqpContextAccessor : IAmqpContextAccessor
{
    private static readonly AsyncLocal<IAmqpContext> s_current = new();

    /// <inheritdoc />
    public IAmqpContext Current
    {
        get => s_current.Value;
        set => s_current.Value = value;
    }
}
