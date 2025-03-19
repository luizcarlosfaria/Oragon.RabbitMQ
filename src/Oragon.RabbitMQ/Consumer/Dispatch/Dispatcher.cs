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
    private readonly List<IAmqpArgumentBinder> argumentBinders;
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

        this.argumentBinders = this.handler.Method.GetParameters().Select(this.BuildArgumentBinder).ToList();

        var messageObjectCount = this.argumentBinders.Count(it => it is MessageObjectArgumentBinder);

        if (messageObjectCount > 1) throw new InvalidOperationException("Only one parameter can be a message object");

        if (messageObjectCount == 0) throw new InvalidOperationException("Not found any parameter to represent a message object");

        this.ReturnType = this.handler.Method.ReturnType;

        this.MessageType = this.argumentBinders.OfType<MessageObjectArgumentBinder>().Single().Type;

        this.resultHandler = this.FindBestResultHandler();
    }

    private IAmqpArgumentBinder BuildArgumentBinder(ParameterInfo parameter)
    {
        if (parameter.IsOut) throw new InvalidOperationException($"The parameter {parameter.Name} is out");

        IAmqpArgumentBinderParameter[] attributes = parameter.GetCustomAttributes(true).OfType<IAmqpArgumentBinderParameter>().ToArray();

        if (attributes.Length > 1) throw new InvalidOperationException($"The parameter {parameter.Name} has more than one IAmqpArgumentBinderParameter attribute");

        return attributes.Length == 1
            ? attributes[0].Build(parameter)
            : DiscoveryArgumentBinder(parameter);
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

        return parameter.ParameterType switch
        {
            var type when type == Constants.IConnection => new DynamicArgumentBinder(context => context.Connection),
            var type when type == Constants.IChannel => new DynamicArgumentBinder(context => context.Channel),
            var type when type == Constants.BasicDeliverEventArgs => new DynamicArgumentBinder(context => context.Request),
            var type when type == Constants.DeliveryMode => new DynamicArgumentBinder(context => context.Request.BasicProperties.DeliveryMode),
            var type when type == Constants.ServiceProvider => new DynamicArgumentBinder(context => context.ServiceProvider),
            var type when type == Constants.IAmqpContext => new DynamicArgumentBinder(context => context),
            var type when type == Constants.BasicPropertiesType => new DynamicArgumentBinder(context => context.Request.BasicProperties),
            var type when type == Constants.CancellationToken => new DynamicArgumentBinder(context => context.CancellationToken),
            var type when type == Constants.String => parameter.Name switch
            {
                var name when Constants.QueueNameParams.Contains(name) => new DynamicArgumentBinder(context => context.QueueName),
                var name when Constants.ExchangeNameParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.Exchange),
                var name when Constants.RoutingKeyNameParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.RoutingKey),
                var name when Constants.ConsumerTagParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.ConsumerTag),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },
            _ => new MessageObjectArgumentBinder(parameter.ParameterType)
        };
    }


    private IResultHandler FindBestResultHandler()
    {
        Type amqpResultType = typeof(IAmqpResult);
        Type taskType = typeof(Task);

        var isTask = this.ReturnType.IsAssignableTo(taskType);
        if (isTask)
        {
            if (this.ReturnType.IsGenericType && this.ReturnType.GenericTypeArguments.Length == 1)
            {
                Type taskValueType = this.ReturnType.GenericTypeArguments[0];
                if (taskValueType.IsAssignableTo(amqpResultType))
                {
                    return new TaskOfAmqpResultResultHandler(this.consumerDescriptor, this.ReturnType);
                }
            }
            return new TaskResultHandler(this.consumerDescriptor);
        }
        else
        {
            return this.ReturnType == typeof(void)
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
            return this.consumerDescriptor.ResultForProcessFailure(context, exception);
        }

    }
}
