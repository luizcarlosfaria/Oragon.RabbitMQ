// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;

/// <summary>
/// Handle Void Results
/// </summary>
public class VoidResultHandler : IResultHandler
{
    private readonly IAMQPResult ack = new AckResult();

    /// <summary>
    /// Handle a task
    /// </summary>
    /// <param name="dispatchResult"></param>
    /// <returns></returns>
    public Task<IAMQPResult> Handle(object dispatchResult)
    {
        return dispatchResult is IAMQPResult simpleAmqpResult
            ? Task.FromResult(simpleAmqpResult)
            : Task.FromResult<IAMQPResult>(this.ack);
    }
}
