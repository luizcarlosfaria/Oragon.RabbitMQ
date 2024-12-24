// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Reflection;
using Dawn;
using Oragon.RabbitMQ.Consumer.Actions;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Consumer.ResultHandlers;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Represents a dispatcher that dispatches messages to a handler.
/// </summary>
public class Dispatcher
{
    private readonly Delegate handler;
    private readonly List<IAmqpArgumentBinder> argumentBinders = [];
    private readonly IResultHandler resultHandler;

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
    public Dispatcher(Delegate handler)
    {
        this.handler = Guard.Argument(handler).NotNull().Value;

        this.argumentBinders = this.handler.Method.GetParameters().Select(this.BuildArgumentBinder).ToList();

        var messageObjectCount = this.argumentBinders.Where(it => it is MessageObjectArgumentBinder).Count();

        if (messageObjectCount > 1) throw new InvalidOperationException("Only one parameter can be a message object");

        if (messageObjectCount == 0) throw new InvalidOperationException("Not found any parameter to represent a message object");

        this.ReturnType = this.handler.Method.ReturnType;

        this.MessageType = this.argumentBinders.OfType<MessageObjectArgumentBinder>().Single().Type;

        this.resultHandler = FindBestResultHandler(type: this.ReturnType);
    }

    private IAmqpArgumentBinder BuildArgumentBinder(ParameterInfo parameter)
    {
        if (parameter.IsOut) throw new InvalidOperationException($"The parameter {parameter.Name} is out");

        IAmqpArgumentBinderParameter[] attributes = parameter.GetCustomAttributes(true).OfType<IAmqpArgumentBinderParameter>().ToArray();

        return attributes.Length > 1
            ? throw new InvalidOperationException($"The parameter {parameter.Name} has more than one attribute")
            : attributes.Length == 0
            ? DiscoveryArgumentBinder(parameter)
            : attributes[0].Build(parameter);
    }

    /// <summary>
    /// Discovery the argument binder
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0046:Convert to conditional expression", Justification = "<Pending>")]
    private static IAmqpArgumentBinder DiscoveryArgumentBinder(ParameterInfo parameter)
    {
        _ = Guard.Argument(parameter).NotNull();

        if (parameter.ParameterType == Constants.IConnection) return new DynamicArgumentBinder(context => context.Connection);

        if (parameter.ParameterType == Constants.IChannel) return new DynamicArgumentBinder(context => context.Channel);

        if (parameter.ParameterType == Constants.BasicDeliverEventArgs) return new DynamicArgumentBinder(context => context.Request);

        if (parameter.ParameterType == Constants.DeliveryMode) return new DynamicArgumentBinder(context => context.Request.BasicProperties.DeliveryMode);

        if (parameter.ParameterType == Constants.ServiceProvider) return new DynamicArgumentBinder(context => context.ServiceProvider);

        if (parameter.ParameterType == Constants.BasicPropertiesType) return new DynamicArgumentBinder(context => context.Request.BasicProperties);

        if (parameter.ParameterType == Constants.CancellationToken) return new DynamicArgumentBinder(context => context.CancellationToken);

        if (parameter.ParameterType == Constants.String)
        {
            return Constants.QueueNameParams.Contains(parameter.Name) ? new DynamicArgumentBinder(context => context.QueueName)

                : Constants.ExchangeNameParams.Contains(parameter.Name) ? new DynamicArgumentBinder(context => context.Request.Exchange)

                : Constants.RoutingKeyNameParams.Contains(parameter.Name) ? new DynamicArgumentBinder(context => context.Request.RoutingKey)

                : Constants.ConsumerTagParams.Contains(parameter.Name) ? new DynamicArgumentBinder(context => context.Request.ConsumerTag)

                : throw new InvalidOperationException($"Can't determine binder for {parameter.Name}");
        }

        return new MessageObjectArgumentBinder(parameter.ParameterType);
    }

    /// <summary>
    /// Find the best result handler
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static IResultHandler FindBestResultHandler(Type type)
    {
        _ = Guard.Argument(type).NotNull();

        Type amqpResultType = typeof(IAMQPResult);
        Type taskType = typeof(Task);

        var isTask = type.IsAssignableTo(taskType);
        if (isTask)
        {
            if (type.IsGenericType && type.GenericTypeArguments.Length == 1)
            {
                Type taskValueType = type.GenericTypeArguments[0];
                if (taskValueType.IsAssignableTo(amqpResultType))
                {
                    return new TaskOfAmqpResultResultHandler(type);
                }
            }
            return new TaskResultHandler();
        }
        else
        {
            return type == typeof(void)
                ? new VoidResultHandler()
                : new GenericResultHandler();
        }
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
    public Task<IAMQPResult> DispatchAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return this.resultHandler.Handle(this.DispatchInternal(context));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    private object DispatchInternal(IAmqpContext context)
    {
        var arguments = new object[this.argumentBinders.Count];
        for (int i = 0; i < this.argumentBinders.Count; i++)
        {
            arguments[i] = this.argumentBinders[i].GetValue(context);
        }
        try
        {
            return this.handler.DynamicInvoke(arguments);
        }
        catch
        {
            return new NackResult(false);
        }

    }
}