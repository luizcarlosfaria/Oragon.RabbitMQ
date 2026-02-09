// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Oragon.RabbitMQ.Consumer.ResultHandlers;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Represents a dispatcher that dispatches messages to a handler.
/// </summary>
public class Dispatcher
{
    private readonly Func<object[], object> compiledInvoker;
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
        ArgumentNullException.ThrowIfNull(consumerDescriptor, nameof(consumerDescriptor));
        ArgumentNullException.ThrowIfNull(consumerDescriptor.Handler, nameof(consumerDescriptor.Handler));

        this.consumerDescriptor = consumerDescriptor;

        this.argumentBinders = consumerDescriptor.BuildArgumentBinders();

        this.ReturnType = consumerDescriptor.Handler.Method.ReturnType;

        this.MessageType = this.argumentBinders.OfType<MessageObjectArgumentBinder>().Single().Type;

        this.resultHandler = consumerDescriptor.FindBestResultHandler();

        this.compiledInvoker = BuildCompiledInvoker(consumerDescriptor.Handler);
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Required for AMQP error handling")]
    private object DispatchInternal(IAmqpContext context)
    {
        int count = this.argumentBinders.Count;
        object[] arguments = ArrayPool<object>.Shared.Rent(count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                arguments[i] = this.argumentBinders[i].GetValue(context);
            }
            return this.compiledInvoker(arguments);
        }
        catch (Exception exception)
        {
            context.LogException(exception);
            return this.consumerDescriptor.ResultForProcessFailure(context, exception);
        }
        finally
        {
            Array.Clear(arguments, 0, count);
            ArrayPool<object>.Shared.Return(arguments);
        }
    }

    private static Func<object[], object> BuildCompiledInvoker(Delegate handler)
    {
        MethodInfo method = handler.Method;
        ParameterInfo[] parameters = method.GetParameters();

        // Parameter: object[] args
        ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "args");

        // Build argument expressions: (T0)args[0], (T1)args[1], ...
        var argExpressions = new Expression[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            BinaryExpression arrayAccess = Expression.ArrayIndex(argsParam, Expression.Constant(i));
            argExpressions[i] = Expression.Convert(arrayAccess, parameters[i].ParameterType);
        }

        // Build the call expression
        Expression callExpression;
        if (handler.Target != null)
        {
            // Instance method or closure
            ConstantExpression targetExpression = Expression.Constant(handler.Target);
            callExpression = Expression.Call(targetExpression, method, argExpressions);
        }
        else
        {
            // Static method
            callExpression = Expression.Call(method, argExpressions);
        }

        // Handle return type
        Expression body;
        if (method.ReturnType == typeof(void))
        {
            // Void: execute the call, then return null
            body = Expression.Block(
                typeof(object),
                callExpression,
                Expression.Constant(null, typeof(object))
            );
        }
        else
        {
            // Reference or value type: convert/box to object
            body = Expression.Convert(callExpression, typeof(object));
        }

        var lambda = Expression.Lambda<Func<object[], object>>(body, argsParam);
        return lambda.Compile();
    }
}
