// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

public class TaskResultHandler : IResultHandler
{
    public async Task<IAMQPResult> Handle(object dispatchResult)
    {
        if (dispatchResult is IAMQPResult simpleAmqpResult)
        {
            return simpleAmqpResult;
        }

        try
        {
            await ((Task)dispatchResult).ConfigureAwait(true);
        }
        catch (Exception)
        {
            return new NackResult(false);
        }
        return new AckResult();
    }
}
