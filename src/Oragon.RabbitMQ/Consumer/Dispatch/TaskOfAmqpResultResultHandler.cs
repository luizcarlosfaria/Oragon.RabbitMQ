// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

public class TaskOfAmqpResultResultHandler : IResultHandler
{
    public async Task<IAMQPResult> Handle(object dispatchResult)
    {
        if (dispatchResult is IAMQPResult simpleAmqpResult)
        {
            return simpleAmqpResult;
        }

        var taskToWait = (Task)dispatchResult;

        try
        {
            await taskToWait.ConfigureAwait(true);
        }
        catch (Exception)
        {
            return new NackResult(false);
        }

        var result = (IAMQPResult)dispatchResult.GetType().GetProperty("Result").GetValue(dispatchResult);

        return result;
    }
}
