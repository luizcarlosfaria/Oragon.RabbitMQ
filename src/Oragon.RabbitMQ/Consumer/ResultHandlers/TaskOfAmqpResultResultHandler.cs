// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Dawn;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;


/// <summary>
/// Handles the result of a dispatched task that returns an IAmqpResult.
/// </summary>
public class TaskOfAmqpResultResultHandler : IResultHandler
{
    private readonly PropertyInfo resultProperty;
    private readonly ConsumerParameters consumerParameters;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="consumerParameters"></param>
    /// <param name="type"></param>
    public TaskOfAmqpResultResultHandler(ConsumerParameters consumerParameters, Type type)
    {
        _ = Guard.Argument(type).NotNull();
        this.consumerParameters = consumerParameters;
        this.Type = type;
        this.resultProperty = type.GetProperty("Result");
    }

    /// <summary>
    /// Type
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Handles the dispatched result, which can be either an IAmqpResult or a Task that returns an IAmqpResult.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="dispatchResult">The result of the dispatch, either an IAmqpResult or a Task.</param>
    /// <returns>The IAmqpResult after the task is awaited, or the original IAmqpResult if it was not a task.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    public async Task<IAmqpResult> Handle(IAmqpContext context, object dispatchResult)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult, nameof(dispatchResult));

        if (dispatchResult is IAmqpResult simpleAmqpResult)
        {
            return simpleAmqpResult;
        }

        var taskToWait = (Task)dispatchResult;

        try
        {
            await taskToWait.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return this.consumerParameters.ResultForProcessFailure(context, exception);
        }

        var result = (IAmqpResult)this.resultProperty.GetValue(dispatchResult);

        return result;
    }
}
