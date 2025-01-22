// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;

/// <summary>
/// Handles the result of a dispatched task that returns an IAmqpResult.
/// </summary>
public class GenericResultHandler : IResultHandler
{

    /// <summary>
    /// Handles the dispatched result, which can be either an IAmqpResult or a Task that returns an IAmqpResult.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="dispatchResult"></param>
    /// <returns></returns>
    public Task<IAmqpResult> Handle(IAmqpContext context, object dispatchResult)
    {
        return dispatchResult is IAmqpResult simpleAmqpResult
            ? Task.FromResult(simpleAmqpResult)
            : Task.FromResult<IAmqpResult>(AmqpResults.Ack());
    }
}
