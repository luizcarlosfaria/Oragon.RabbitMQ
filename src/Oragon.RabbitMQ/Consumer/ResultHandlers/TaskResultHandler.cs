// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;

/// <summary>
/// Handles the result of a dispatched task.
/// </summary>
public class TaskResultHandler : IResultHandler
{
    private readonly ConsumerParameters consumerParameters;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="consumerParameters"></param>
    public TaskResultHandler(ConsumerParameters consumerParameters)
    {
        this.consumerParameters = consumerParameters;
    }

    /// <summary>
    /// Handles the dispatched result, which can be either an IAmqpResult or a Task.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="dispatchResult"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    public async Task<IAmqpResult> Handle(IAmqpContext context, object dispatchResult)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult, nameof(dispatchResult));

        _ = Guard.Argument(dispatchResult).NotNull();

        if (dispatchResult is IAmqpResult simpleAmqpResult)
        {
            return simpleAmqpResult;
        }

        try
        {
            await ((Task)dispatchResult).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return this.consumerParameters.ResultForProcessFailure(context, exception);
        }
        return AmqpResults.Ack();
    }
}
