// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ;

#pragma warning disable CS1591, CA1819, CA1720

public static class Constants
{
    public static Type String { get; } = typeof(string);
    public static Type DeliveryMode { get; } = typeof(DeliveryModes);
    public static Type IConnection { get; } = typeof(IConnection);
    public static Type IChannel { get; } = typeof(IChannel);
    public static Type IAmqpResult { get; } = typeof(IAmqpResult);
    public static Type TaskOfIAmqpResult { get; } = typeof(Task<IAmqpResult>);
    public static Type BasicDeliverEventArgs { get; } = typeof(BasicDeliverEventArgs);
    public static Type ServiceProvider { get; } = typeof(IServiceProvider);
    public static Type IAmqpContext { get; } = typeof(Consumer.IAmqpContext);
    public static Type BasicPropertiesType { get; } = typeof(IReadOnlyBasicProperties);
    public static Type CancellationToken { get; } = typeof(CancellationToken);
    public static Type VoidTaskResult { get; } = Type.GetType("System.Threading.Tasks.VoidTaskResult");
    public static Type Task { get; } = typeof(Task);
    public static Type GenericTask { get; } = typeof(Task<>);
    public static Type VoidResult { get; } = typeof(void);
    public static string[] QueueNameParams { get; } = ["queue", "queueName"];
    public static string[] RoutingKeyNameParams { get; } = ["routing", "routingKey"];
    public static string[] ExchangeNameParams { get; } = ["exchange", "exchangeName"];
    public static string[] ConsumerTagParams { get; } = ["consumer", "consumerTag"];

}
#pragma warning restore CS1591, CA1819, CA1720
