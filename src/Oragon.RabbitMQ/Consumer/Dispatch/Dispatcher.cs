// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using Dawn;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Oragon.RabbitMQ.Consumer.ResultHandlers;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Represents a dispatcher that dispatches messages to a handler.
/// </summary>
public class Dispatcher
{
    private readonly Delegate handler;
    private readonly ReadOnlyCollection<IAmqpArgumentBinder> argumentBinders;
    private readonly IResultHandler resultHandler;
    private readonly ConsumerDescriptor consumerDescriptor;

    /// <summary>
    /// Type of Message
    /// </summary>
    public Type MessageType { get; private set; }

    /// <summary>
    /// Type of result of processing 
    /// </summary>
    public Type ReturnType { get; private set; }

    /// <summary>
    /// Initialize the dispatcher
    /// </summary>
    public Dispatcher(ConsumerDescriptor consumerDescriptor)
    {
        this.consumerDescriptor = Guard.Argument(consumerDescriptor).NotNull().Value;

        this.handler = Guard.Argument(consumerDescriptor.Handler).NotNull().Value;

        this.argumentBinders = consumerDescriptor.BuildArgumentBinders();

        this.ReturnType = this.handler.Method.ReturnType;

        this.MessageType = this.argumentBinders.OfType<MessageObjectArgumentBinder>().Single().Type;

        this.resultHandler = consumerDescriptor.FindBestResultHandler();
    }

    /// <summary>
    /// Get the argument binders
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IEnumerable<T> GetArgumentBindersOfType<T>() where T : IAmqpArgumentBinder
    {
        return this.argumentBinders.OfType<T>();
    }

    /// <summary>
    /// Dispatch the message
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public Task<IAmqpResult> DispatchAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return this.resultHandler.Handle(context, this.DispatchInternal(context));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    private object DispatchInternal(IAmqpContext context)
    {
        var arguments = new object[this.argumentBinders.Count];
        for (var i = 0; i < this.argumentBinders.Count; i++)
        {
            arguments[i] = this.argumentBinders[i].GetValue(context);
        }
        try
        {
            return this.handler.DynamicInvoke(arguments);
        }
        catch (Exception exception)
        {
            context.LogException(exception);
            return this.consumerDescriptor.ResultForProcessFailure(context, exception);
        }

    }
}
