// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Reflection;
using Oragon.RabbitMQ.Consumer.ArgumentBinders;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using RabbitMQ.Client;

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

        int messageObjectCount = argumentBinders.Count(it => it is MessageObjectArgumentBinder);

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
            var type when type == Constants.DeliveryMode => OptionalMetadataRequiresNullable(parameter, "deliveryMode", "DeliveryModes?"),
            var type when type == Constants.NullableDeliveryMode => new DynamicArgumentBinder(context => BindDeliveryModeAsNullableDeliveryMode(context)),
            var type when type == Constants.HeadersType => new DynamicArgumentBinder(context => context.Request.BasicProperties.Headers),
            var type when type == Constants.ReadOnlyHeadersType => new DynamicArgumentBinder(context => context.Request.BasicProperties.Headers),
            var type when type == Constants.AmqpTimestampType => OptionalMetadataRequiresNullable(parameter, "timestamp", "AmqpTimestamp?"),
            var type when type == Constants.NullableAmqpTimestampType => new DynamicArgumentBinder(context => BindTimestampAsNullableAmqpTimestamp(context)),
            var type when type == Constants.ServiceProvider => new DynamicArgumentBinder(context => context.ServiceProvider),
            var type when type == Constants.IAmqpContext => new DynamicArgumentBinder(context => context),
            var type when type == Constants.BasicPropertiesType => new DynamicArgumentBinder(context => context.Request.BasicProperties),
            var type when type == Constants.CancellationToken => new DynamicArgumentBinder(context => context.CancellationToken),

            //string values inference by type/name

            //string
            var type when type == Constants.StringType => parameter.Name switch
            {
                var name when Constants.QueueNameParams.Contains(name) => new DynamicArgumentBinder(context => context.QueueName),
                var name when Constants.ExchangeNameParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.Exchange),
                var name when Constants.RoutingKeyNameParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.RoutingKey),
                var name when Constants.ConsumerTagParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.ConsumerTag),
                var name when Constants.ContentTypeParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.ContentType),
                var name when Constants.ContentEncodingParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.ContentEncoding),
                var name when Constants.CorrelationIdParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.CorrelationId),
                var name when Constants.ReplyToParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.ReplyTo),
                var name when Constants.ExpirationParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.Expiration),
                var name when Constants.MessageIdParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.MessageId),
                var name when Constants.TypeParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.Type),
                var name when Constants.UserIdParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.UserId),
                var name when Constants.AppIdParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.AppId),
                var name when Constants.ClusterIdParams.Contains(name) => new DynamicArgumentBinder(context => context.Request.BasicProperties.ClusterId),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            //byte
            var type when type == Constants.ByteType => parameter.Name switch
            {
                var name when Constants.PriorityParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "priority", "byte?, int? or long?"),
                var name when Constants.DeliveryModeParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "deliveryMode", "DeliveryModes?, byte?, int? or long?"),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            //byte?
            var type when type == Constants.NullableByteType => parameter.Name switch
            {
                var name when Constants.PriorityParams.Contains(name) => new DynamicArgumentBinder(context => BindPriorityAsNullableByte(context)),
                var name when Constants.DeliveryModeParams.Contains(name) => new DynamicArgumentBinder(context => BindDeliveryModeAsNullableByte(context)),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            //int
            var type when type == Constants.IntType => parameter.Name switch
            {
                var name when Constants.PriorityParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "priority", "byte?, int? or long?"),
                var name when Constants.DeliveryModeParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "deliveryMode", "DeliveryModes?, byte?, int? or long?"),
                var name when Constants.DeliveryCountParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "deliveryCount", "int? or long?"),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            //long
            var type when type == Constants.LongType => parameter.Name switch
            {
                var name when Constants.PriorityParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "priority", "byte?, int? or long?"),
                var name when Constants.DeliveryModeParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "deliveryMode", "DeliveryModes?, byte?, int? or long?"),
                var name when Constants.TimestampParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "timestamp", "DateTimeOffset?, long? or AmqpTimestamp?"),
                var name when Constants.DeliveryCountParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "deliveryCount", "int? or long?"),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            //int?
            var type when type == Constants.NullableIntType => parameter.Name switch
            {
                var name when Constants.PriorityParams.Contains(name) => new DynamicArgumentBinder(context => (int?)BindPriorityAsNullableByte(context)),
                var name when Constants.DeliveryModeParams.Contains(name) => new DynamicArgumentBinder(context => (int?)BindDeliveryModeAsNullableByte(context)),
                var name when Constants.DeliveryCountParams.Contains(name) => new DynamicArgumentBinder(context => (int?)AmqpHeaders.GetDeliveryCount(context.Request.BasicProperties)),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            //long?
            var type when type == Constants.NullableLongType => parameter.Name switch
            {
                var name when Constants.PriorityParams.Contains(name) => new DynamicArgumentBinder(context => (long?)BindPriorityAsNullableByte(context)),
                var name when Constants.DeliveryModeParams.Contains(name) => new DynamicArgumentBinder(context => (long?)BindDeliveryModeAsNullableByte(context)),
                var name when Constants.TimestampParams.Contains(name) => new DynamicArgumentBinder(context => BindTimestampAsNullableUnixTime(context)),
                var name when Constants.DeliveryCountParams.Contains(name) => new DynamicArgumentBinder(context => AmqpHeaders.GetDeliveryCount(context.Request.BasicProperties)),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            // Guid
            var type when type == Constants.GuidType => parameter.Name switch
            {
                var name when Constants.MessageIdParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "messageId", "string or Guid?"),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            // Guid?
            var type when type == Constants.NullableGuidType => parameter.Name switch
            {
                var name when Constants.MessageIdParams.Contains(name) => new DynamicArgumentBinder(context => BindMessageIdAsNullableGuid(context)),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            // DateTimeOffset
            var type when type == Constants.DateTimeOffsetType => parameter.Name switch
            {
                var name when Constants.TimestampParams.Contains(name) => OptionalMetadataRequiresNullable(parameter, "timestamp", "DateTimeOffset?, long? or AmqpTimestamp?"),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            // DateTimeOffset?
            var type when type == Constants.NullableDateTimeOffsetType => parameter.Name switch
            {
                var name when Constants.TimestampParams.Contains(name) => new DynamicArgumentBinder(context => BindTimestampAsNullableDateTimeOffset(context)),
                _ => throw new InvalidOperationException($"Can't determine binder for {parameter.Name}")
            },

            //inference by default
            _ => new MessageObjectArgumentBinder(parameter.ParameterType)
        };
    }

    private static Guid? BindMessageIdAsNullableGuid(IAmqpContext context)
    {
        string messageId = context.Request.BasicProperties.MessageId;
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        if (Guid.TryParse(messageId, out Guid value))
        {
            return value;
        }

        return null;
    }

    private static IAmqpArgumentBinder OptionalMetadataRequiresNullable(ParameterInfo parameter, string propertyName, string nullableTypes)
    {
        throw new InvalidOperationException($"Can't bind {parameter.Name}: AMQP {propertyName} is optional. Use {nullableTypes}.");
    }

    private static byte? BindPriorityAsNullableByte(IAmqpContext context)
    {
        IReadOnlyBasicProperties properties = context.Request.BasicProperties;
        if (!properties.IsPriorityPresent())
        {
            return null;
        }

        return properties.Priority;
    }

    private static DeliveryModes? BindDeliveryModeAsNullableDeliveryMode(IAmqpContext context)
    {
        IReadOnlyBasicProperties properties = context.Request.BasicProperties;
        if (!properties.IsDeliveryModePresent())
        {
            return null;
        }

        return properties.DeliveryMode;
    }

    private static byte? BindDeliveryModeAsNullableByte(IAmqpContext context)
    {
        DeliveryModes? deliveryMode = BindDeliveryModeAsNullableDeliveryMode(context);
        return deliveryMode.HasValue ? (byte)deliveryMode.Value : null;
    }

    private static long? BindTimestampAsNullableUnixTime(IAmqpContext context)
    {
        IReadOnlyBasicProperties properties = context.Request.BasicProperties;
        if (!properties.IsTimestampPresent())
        {
            return null;
        }

        return properties.Timestamp.UnixTime;
    }

    private static AmqpTimestamp? BindTimestampAsNullableAmqpTimestamp(IAmqpContext context)
    {
        IReadOnlyBasicProperties properties = context.Request.BasicProperties;
        if (!properties.IsTimestampPresent())
        {
            return null;
        }

        return properties.Timestamp;
    }

    private static DateTimeOffset? BindTimestampAsNullableDateTimeOffset(IAmqpContext context)
    {
        IReadOnlyBasicProperties properties = context.Request.BasicProperties;
        if (!properties.IsTimestampPresent())
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime);
    }

}
