// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ;

/// <summary>
/// Cached type references and parameter name conventions used for argument binding inference.
/// </summary>
public static class Constants
{
    /// <summary>Cached type reference for <see cref="byte"/>.</summary>
    public static Type ByteType { get; } = typeof(byte);

    /// <summary>Cached type reference for <see cref="Nullable{T}"/> of <see cref="byte"/>.</summary>
    public static Type NullableByteType { get; } = typeof(byte?);

    /// <summary>Cached type reference for <see cref="long"/>.</summary>
    public static Type LongType { get; } = typeof(long);

    /// <summary>Cached type reference for <see cref="Nullable{T}"/> of <see cref="long"/>.</summary>
    public static Type NullableLongType { get; } = typeof(long?);

    /// <summary>Cached type reference for <see cref="int"/>.</summary>
    public static Type IntType { get; } = typeof(int);

    /// <summary>Cached type reference for <see cref="Nullable{T}"/> of <see cref="int"/>.</summary>
    public static Type NullableIntType { get; } = typeof(int?);

    /// <summary>Cached type reference for <see cref="string"/>.</summary>
    public static Type StringType { get; } = typeof(string);

    /// <summary>Cached type reference for <see cref="Guid"/>.</summary>
    public static Type GuidType { get; } = typeof(Guid);

    /// <summary>Cached type reference for <see cref="Nullable{T}"/> of <see cref="Guid"/>.</summary>
    public static Type NullableGuidType { get; } = typeof(Guid?);

    /// <summary>Cached type reference for <see cref="DateTimeOffset"/>.</summary>
    public static Type DateTimeOffsetType { get; } = typeof(DateTimeOffset);

    /// <summary>Cached type reference for <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>.</summary>
    public static Type NullableDateTimeOffsetType { get; } = typeof(DateTimeOffset?);

    /// <summary>Cached type reference for <see cref="IDictionary{TKey,TValue}"/> of string/object.</summary>
    public static Type HeadersType { get; } = typeof(IDictionary<string, object>);

    /// <summary>Cached type reference for <see cref="IReadOnlyDictionary{TKey,TValue}"/> of string/object.</summary>
    public static Type ReadOnlyHeadersType { get; } = typeof(IReadOnlyDictionary<string, object>);

    /// <summary>Cached type reference for <see cref="AmqpTimestamp"/>.</summary>
    public static Type AmqpTimestampType { get; } = typeof(AmqpTimestamp);

    /// <summary>Cached type reference for <see cref="Nullable{T}"/> of <see cref="AmqpTimestamp"/>.</summary>
    public static Type NullableAmqpTimestampType { get; } = typeof(AmqpTimestamp?);

    /// <summary>Cached type reference for <see cref="DeliveryModes"/>.</summary>
    public static Type DeliveryModeType { get; } = typeof(DeliveryModes);

    /// <summary>Parameter name recognized as delivery mode by convention.</summary>
    public const string DeliveryModePropertyName = "deliveryMode";

    /// <summary>Cached type reference for <see cref="Nullable{T}"/> of <see cref="DeliveryModes"/>.</summary>
    public static Type NullableDeliveryMode { get; } = typeof(DeliveryModes?);

    /// <summary>Cached type reference for RabbitMQ.Client.IConnection.</summary>
    public static Type IConnection { get; } = typeof(IConnection);

    /// <summary>Cached type reference for RabbitMQ.Client.IChannel.</summary>
    public static Type IChannel { get; } = typeof(IChannel);

    /// <summary>Cached type reference for <see cref="IAmqpResult"/>.</summary>
    public static Type IAmqpResult { get; } = typeof(IAmqpResult);

    /// <summary>Cached type reference for <see cref="Task{IAmqpResult}"/>.</summary>
    public static Type TaskOfIAmqpResult { get; } = typeof(Task<IAmqpResult>);

    /// <summary>Cached type reference for RabbitMQ.Client.Events.BasicDeliverEventArgs.</summary>
    public static Type BasicDeliverEventArgs { get; } = typeof(BasicDeliverEventArgs);

    /// <summary>Cached type reference for <see cref="IServiceProvider"/>.</summary>
    public static Type ServiceProvider { get; } = typeof(IServiceProvider);

    /// <summary>Cached type reference for <see cref="Consumer.IAmqpContext"/>.</summary>
    public static Type IAmqpContext { get; } = typeof(Consumer.IAmqpContext);

    /// <summary>Cached type reference for <see cref="IReadOnlyBasicProperties"/>.</summary>
    public static Type BasicPropertiesType { get; } = typeof(IReadOnlyBasicProperties);

    /// <summary>Cached type reference for <see cref="System.Threading.CancellationToken"/>.</summary>
    public static Type CancellationToken { get; } = typeof(CancellationToken);

    /// <summary>Cached type reference for the internal VoidTaskResult type.</summary>
    public static Type VoidTaskResult { get; } = Type.GetType("System.Threading.Tasks.VoidTaskResult");

    /// <summary>Cached type reference for <see cref="System.Threading.Tasks.Task"/>.</summary>
    public static Type Task { get; } = typeof(Task);

    /// <summary>Cached type reference for the open generic <see cref="Task{TResult}"/>.</summary>
    public static Type GenericTask { get; } = typeof(Task<>);

    /// <summary>Cached type reference for <see langword="void"/>.</summary>
    public static Type VoidResult { get; } = typeof(void);

    /// <summary>AMQP header that carries the delivery count on quorum queues.</summary>
    public const string XDeliveryCountHeader = "x-delivery-count";

    /// <summary>Parameter names recognized as priority by convention.</summary>
    public static ReadOnlyCollection<string> PriorityParams { get; } = Array.AsReadOnly(["priority"]);

    /// <summary>Parameter names recognized as content type by convention.</summary>
    public static ReadOnlyCollection<string> ContentTypeParams { get; } = Array.AsReadOnly(["contentType"]);

    /// <summary>Parameter names recognized as content encoding by convention.</summary>
    public static ReadOnlyCollection<string> ContentEncodingParams { get; } = Array.AsReadOnly(["contentEncoding"]);

    /// <summary>Parameter names recognized as delivery mode by convention.</summary>
    public static ReadOnlyCollection<string> DeliveryModeParams { get; } = Array.AsReadOnly(["deliveryMode"]);

    /// <summary>Parameter names recognized as correlation id by convention.</summary>
    public static ReadOnlyCollection<string> CorrelationIdParams { get; } = Array.AsReadOnly(["correlationId"]);

    /// <summary>Parameter names recognized as reply-to by convention.</summary>
    public static ReadOnlyCollection<string> ReplyToParams { get; } = Array.AsReadOnly(["replyTo"]);

    /// <summary>Parameter names recognized as expiration by convention.</summary>
    public static ReadOnlyCollection<string> ExpirationParams { get; } = Array.AsReadOnly(["expiration"]);

    /// <summary>Parameter names recognized as message id by convention.</summary>
    public static ReadOnlyCollection<string> MessageIdParams { get; } = Array.AsReadOnly(["messageId"]);

    /// <summary>Parameter names recognized as timestamp by convention.</summary>
    public static ReadOnlyCollection<string> TimestampParams { get; } = Array.AsReadOnly(["timestamp"]);

    /// <summary>Parameter names recognized as message type by convention.</summary>
    public static ReadOnlyCollection<string> TypeParams { get; } = Array.AsReadOnly(["type", "messageType"]);

    /// <summary>Parameter names recognized as user id by convention.</summary>
    public static ReadOnlyCollection<string> UserIdParams { get; } = Array.AsReadOnly(["userId"]);

    /// <summary>Parameter names recognized as app id by convention.</summary>
    public static ReadOnlyCollection<string> AppIdParams { get; } = Array.AsReadOnly(["appId"]);

    /// <summary>Parameter names recognized as cluster id by convention.</summary>
    public static ReadOnlyCollection<string> ClusterIdParams { get; } = Array.AsReadOnly(["clusterId"]);

    /// <summary>Parameter names recognized as delivery count (<c>x-delivery-count</c> header) by convention.</summary>
    public static ReadOnlyCollection<string> DeliveryCountParams { get; } = Array.AsReadOnly(["deliveryCount", "attempts"]);

    /// <summary>Parameter names recognized as queue name by convention.</summary>
    public static ReadOnlyCollection<string> QueueNameParams { get; } = Array.AsReadOnly(["queue", "queueName"]);

    /// <summary>Parameter names recognized as routing key by convention.</summary>
    public static ReadOnlyCollection<string> RoutingKeyNameParams { get; } = Array.AsReadOnly(["routing", "routingKey"]);

    /// <summary>Parameter names recognized as exchange name by convention.</summary>
    public static ReadOnlyCollection<string> ExchangeNameParams { get; } = Array.AsReadOnly(["exchange", "exchangeName"]);

    /// <summary>Parameter names recognized as consumer tag by convention.</summary>
    public static ReadOnlyCollection<string> ConsumerTagParams { get; } = Array.AsReadOnly(["consumer", "consumerTag"]);

    /// <summary>ASP.NET MVC attribute type names that are prohibited on handler parameters.</summary>
    public static ReadOnlyCollection<string> MvcAttributesTypeNames { get; } = Array.AsReadOnly(["FromBodyAttribute", "FromFormAttribute", "FromHeaderAttribute", "FromQueryAttribute", "FromRouteAttribute", "FromServicesAttribute"]);

    /// <summary>ASP.NET MVC namespaces used to detect prohibited attributes.</summary>
    public static ReadOnlyCollection<string> MvcAttributeNamespaces { get; } = Array.AsReadOnly(["Microsoft.AspNetCore.Mvc"]);
}
