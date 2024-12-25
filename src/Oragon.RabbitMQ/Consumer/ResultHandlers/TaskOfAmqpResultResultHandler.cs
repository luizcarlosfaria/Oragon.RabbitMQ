// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Dawn;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;


/// <summary>
/// Handles the result of a dispatched task that returns an IAMQPResult.
/// </summary>
public class TaskOfAmqpResultResultHandler : IResultHandler
{
    private readonly IAMQPResult nack = new NackResult(false);
    private readonly PropertyInfo resultProperty;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="type"></param>
    public TaskOfAmqpResultResultHandler(Type type)
    {
        _ = Guard.Argument(type).NotNull();

        this.Type = type;
        this.resultProperty = type.GetProperty("Result");
    }

    /// <summary>
    /// Type
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Handles the dispatched result, which can be either an IAMQPResult or a Task that returns an IAMQPResult.
    /// </summary>
    /// <param name="dispatchResult">The result of the dispatch, either an IAMQPResult or a Task.</param>
    /// <returns>The IAMQPResult after the task is awaited, or the original IAMQPResult if it was not a task.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    public async Task<IAMQPResult> Handle(object dispatchResult)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult, nameof(dispatchResult));

        if (dispatchResult is IAMQPResult simpleAmqpResult)
        {
            return simpleAmqpResult;
        }

        var taskToWait = (Task)dispatchResult;

        try
        {
            await taskToWait.ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return this.nack;
        }
        catch (OperationCanceledException)
        {
            return this.nack;
        }
        catch
        {
            return this.nack;
        }

        var result = (IAMQPResult)this.resultProperty.GetValue(dispatchResult);

        return result;
    }
}
