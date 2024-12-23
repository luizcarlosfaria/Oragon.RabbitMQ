// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.Dispatch;


/// <summary>
/// Handles the result of a dispatched task that returns an IAMQPResult.
/// </summary>
public class TaskOfAmqpResultResultHandler : IResultHandler
{
    /// <summary>
    /// Handles the dispatched result, which can be either an IAMQPResult or a Task that returns an IAMQPResult.
    /// </summary>
    /// <param name="dispatchResult">The result of the dispatch, either an IAMQPResult or a Task.</param>
    /// <returns>The IAMQPResult after the task is awaited, or the original IAMQPResult if it was not a task.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    public async Task<IAMQPResult> Handle(object dispatchResult)
    {
        _ = Guard.Argument(dispatchResult).NotNull();

        if (dispatchResult is IAMQPResult simpleAmqpResult)
        {
            return simpleAmqpResult;
        }

        var taskToWait = (Task)dispatchResult;

        try
        {
            await taskToWait.ConfigureAwait(true);
        }
        catch
        {
            return new NackResult(false);
        }

        var result = (IAMQPResult)dispatchResult.GetType().GetProperty("Result").GetValue(dispatchResult);

        return result;
    }
}
