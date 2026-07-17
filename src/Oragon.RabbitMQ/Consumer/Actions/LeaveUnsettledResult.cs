// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Leaves the current delivery unsettled.
/// </summary>
public sealed class LeaveUnsettledResult : IAmqpResult
{
    internal LeaveUnsettledResult()
    {
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.CompletedTask;
    }
}
