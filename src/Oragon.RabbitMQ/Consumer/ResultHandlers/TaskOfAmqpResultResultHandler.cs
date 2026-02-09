// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Linq.Expressions;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;


/// <summary>
/// Handles the result of a dispatched task that returns an IAmqpResult.
/// </summary>
public class TaskOfAmqpResultResultHandler : IResultHandler
{
    private readonly Func<object, IAmqpResult> resultExtractor;
    private readonly ConsumerDescriptor consumerDescriptor;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="consumerDescriptor"></param>
    /// <param name="type"></param>
    public TaskOfAmqpResultResultHandler(ConsumerDescriptor consumerDescriptor, Type type)
    {
        ArgumentNullException.ThrowIfNull(consumerDescriptor);
        ArgumentNullException.ThrowIfNull(type);

        this.consumerDescriptor = consumerDescriptor;
        this.Type = type;

        _ = type.GetProperty("Result")
            ?? throw new InvalidOperationException($"Type {type.FullName} does not have a 'Result' property.");

        this.resultExtractor = BuildResultExtractor(type);
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Required for AMQP error handling")]
    public async Task<IAmqpResult> Handle(IAmqpContext context, object dispatchResult)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(dispatchResult, nameof(dispatchResult));

        if (dispatchResult is IAmqpResult simpleAmqpResult)
        {
            return simpleAmqpResult;
        }

        var taskToWait = (Task)dispatchResult;

        try
        {
            await taskToWait.ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            context.LogException(exception);
            return this.consumerDescriptor.ResultForProcessFailure(context, exception);
        }

        var result = this.resultExtractor(dispatchResult)
            ?? throw new InvalidOperationException(
                $"Handler returned null from Task<IAmqpResult>. Queue: {context.QueueName}");

        return result;
    }

    /// <summary>
    /// Builds a compiled delegate that extracts .Result from a Task&lt;T&gt; and casts to IAmqpResult.
    /// </summary>
    private static Func<object, IAmqpResult> BuildResultExtractor(Type taskType)
    {
        // (object task) => (IAmqpResult)((Task<T>)task).Result
        var taskParam = Expression.Parameter(typeof(object), "task");
        var castToTask = Expression.Convert(taskParam, taskType);
        var resultAccess = Expression.Property(castToTask, "Result");
        var castToIAmqpResult = Expression.Convert(resultAccess, typeof(IAmqpResult));
        var lambda = Expression.Lambda<Func<object, IAmqpResult>>(castToIAmqpResult, taskParam);
        return lambda.Compile();
    }
}
