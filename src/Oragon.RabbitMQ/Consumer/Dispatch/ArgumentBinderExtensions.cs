// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Reflection;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;

namespace Oragon.RabbitMQ.Consumer.Dispatch;


/// <summary>
/// Generates a list of argument binders from a consumer descriptor's handler method parameters. It ensures only one
/// message object parameter is present.
/// </summary>
internal static class ArgumentBinderExtensions
{
    /// <summary>
    /// Creates a list of argument binders based on the provided consumer descriptor's handler method parameters.
    /// </summary>
    /// <param name="consumerDescriptor">Describes the consumer and its associated handler for which argument binders are being built.</param>
    /// <returns>A list of argument binders corresponding to the parameters of the handler method.</returns>
    /// <exception cref="InvalidOperationException">Thrown when there are either multiple message object parameters or none found.</exception>
    internal static ReadOnlyCollection<IAmqpArgumentBinder> BuildArgumentBinders(this ConsumerDescriptor consumerDescriptor)
    {
        ArgumentNullException.ThrowIfNull(consumerDescriptor);
        ArgumentNullException.ThrowIfNull(consumerDescriptor.Handler);


        var argumentBinders = consumerDescriptor.Handler.Method.GetParameters().Select(BuildArgumentBinder).ToList();

        var messageObjectCount = argumentBinders.Count(it => it is MessageObjectArgumentBinder);

        if (messageObjectCount > 1) throw new InvalidOperationException("Only one parameter can be a message object");

        if (messageObjectCount == 0) throw new InvalidOperationException("Not found any parameter to represent a message object");

        return argumentBinders.AsReadOnly();
    }

    private static IAmqpArgumentBinder BuildArgumentBinder(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        if (parameter.IsOut) throw new InvalidOperationException($"The parameter {parameter.Name} is out. Out parameter is not supported.");

        ValidateIfContainsAspNetMvcAttributes(parameter);

        IAmqpArgumentBinderParameter attribute = GetAmqpArgumentBinderParameter(parameter);

        return attribute != null
            ? attribute.Build(parameter) //Use the attribute to build the binder
            : DiscoveryArgumentBinder(parameter); //Discovery the binder by inference
    }

    private static IAmqpArgumentBinderParameter GetAmqpArgumentBinderParameter(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        IAmqpArgumentBinderParameter[] attributes = parameter.GetCustomAttributes(true)
                                                            .OfType<IAmqpArgumentBinderParameter>()
                                                            .ToArray();

        if (attributes.Length > 1) throw new InvalidOperationException($"The parameter {parameter.Name} has more than one IAmqpArgumentBinderParameter attribute");

        return attributes.FirstOrDefault();//Can be null
    }

    private static void ValidateIfContainsAspNetMvcAttributes(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        List<Type> allAttributes = [.. parameter.GetCustomAttributes(true).Select(it => it.GetType())];

        if (allAttributes.Exists(it => Constants.MvcAttributesTypeNames.Contains(it.Name) && Constants.MvcAttributeNamespaces.Contains(it.Namespace)))
            throw new InvalidOperationException($"The parameter {parameter.Name} has an attribute ({string.Join(", ", Constants.MvcAttributesTypeNames)}) from ASP.NET MVC namespaces {string.Join(" or ", Constants.MvcAttributeNamespaces)}.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0046:Convert to conditional expression", Justification = "<Pending>")]
    private static IAmqpArgumentBinder DiscoveryArgumentBinder(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return parameter.ParameterType switch
        {
            //inference by type
            var type when type == Constants.IConnection => new DynamicArgumentBinder(context => context.Connection),
            var type when type == Constants.IChannel => new DynamicArgumentBinder(context => context.Channel),
            var type when type == Constants.BasicDeliverEventArgs => new DynamicArgumentBinder(context => context.Request),
            var type when type == Constants.DeliveryMode => new DynamicArgumentBinder(context => context.Request.BasicProperties.DeliveryMode),
            var type when type == Constants.ServiceProvider => new DynamicArgumentBinder(context => context.ServiceProvider),
            var type when type == Constants.IAmqpContext => new DynamicArgumentBinder(context => context),
            var type when type == Constants.BasicPropertiesType => new DynamicArgumentBinder(context => context.Request.BasicProperties),
            var type when type == Constants.CancellationToken => new DynamicArgumentBinder(context => context.CancellationToken),
            //inference by name
            var type when type == Constants.String => parameter.Name switch
            {
                var name when Constants.QueueNameParams.Contains(name) => new DynamicArgumentBinder(context => context.QueueName),
                var name when Constants.ExchangeNameParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.Exchange),
                var name when Constants.RoutingKeyNameParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.RoutingKey),
                var name when Constants.ConsumerTagParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.ConsumerTag),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },
            //inference by default
            _ => new MessageObjectArgumentBinder(parameter.ParameterType)
        };
    }




}
