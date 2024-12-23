// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Handles the result of a dispatched task that returns an IAMQPResult.
/// </summary>
public class GenericResultHandler : IResultHandler
{
    /// <summary>
    /// Handles the dispatched result, which can be either an IAMQPResult or a Task that returns an IAMQPResult.
    /// </summary>
    /// <param name="dispatchResult"></param>
    /// <returns></returns>
    public Task<IAMQPResult> Handle(object dispatchResult)
    {
        return dispatchResult is IAMQPResult simpleAmqpResult
            ? Task.FromResult(simpleAmqpResult)
            : Task.FromResult<IAMQPResult>(new AckResult());
    }
}
