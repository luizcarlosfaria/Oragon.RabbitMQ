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

    /// <summary>Cached type reference for <see cref="DeliveryModes"/>.</summary>
    public static Type DeliveryMode { get; } = typeof(DeliveryModes);

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
