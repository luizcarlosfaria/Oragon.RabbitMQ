// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.



using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer.Actions;

/// <summary>
/// Construct and cache results
/// </summary>
public static class AmqpResults
{

    private static readonly AckResult s_forSuccess = new();

    private static readonly NackResult s_nackWithRequeue = new(true);

    private static readonly NackResult s_nackWithoutRequeue = new(false);

    private static readonly RejectResult s_rejectWithRequeue = new(true);

    private static readonly RejectResult s_rejectWithoutRequeue = new(false);

    private static readonly LeaveUnsettledResult s_leaveUnsettled = new();

    /// <summary>
    /// Return an AckResult to represents a AMQP Ack
    /// </summary>
    /// <returns></returns>
    public static AckResult Ack() => AmqpResults.s_forSuccess;


    /// <summary>
    /// Return an NackResult to represents a AMQP Nack
    /// </summary>
    /// <returns></returns>
    public static NackResult Nack(bool requeue) =>
        requeue
        ? AmqpResults.s_nackWithRequeue
        : AmqpResults.s_nackWithoutRequeue;

    /// <summary>
    /// Return an RejectResult to represents a AMQP Reject
    /// </summary>
    /// <returns></returns>
    public static RejectResult Reject(bool requeue) =>
        requeue
        ? AmqpResults.s_rejectWithRequeue
        : AmqpResults.s_rejectWithoutRequeue;

    /// <summary>
    /// Return a result that leaves the current delivery unsettled.
    /// </summary>
    /// <returns></returns>
    public static LeaveUnsettledResult LeaveUnsettled() => AmqpResults.s_leaveUnsettled;


    /// <summary>
    /// Return a ReplyResult to represents a AMQP Reply
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="objectToReturn"></param>
    /// <returns></returns>
    public static ReplyResult<T> Reply<T>(T objectToReturn) => new(objectToReturn);


    /// <summary>
    /// Return a ComposableResult with ReplyResult and AckResult
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="objectToReturn"></param>k
    /// <returns></returns>
    public static ComposableResult ReplyAndAck<T>(T objectToReturn) => new(new ReplyResult<T>(objectToReturn), s_forSuccess);

    /// <summary>
    /// Return a ForwardResult to represents a AMQP Forward to another exchange and routing key
    /// </summary>
    /// <param name="exchange">The name of the exchange to which the objects will be forwarded. Cannot be <see langword="null"/>.</param>
    /// <param name="routingKey">The routing key used to route the forwarded objects. Cannot be <see langword="null"/>.</param>
    /// <param name="mandatory">A value indicating whether the forwarding operation is mandatory. If <see langword="true"/>, the operation
    /// requires confirmation that the message was routed successfully.</param>
    /// <param name="basicPropertiesConfigureAction"></param>    
    /// <param name="objectsToForward">The objects to be forwarded. Cannot be <see langword="null"/> and must contain at least one object.</param>

    public static ForwardResult<T> Forward<T>(string exchange, string routingKey, bool mandatory, Action<T, IBasicProperties> basicPropertiesConfigureAction = null, params T[] objectsToForward)
        => new(exchange, routingKey, mandatory, basicPropertiesConfigureAction, objectsToForward);

    /// <summary>
    /// Return a ForwardResult to represents a AMQP Forward to another exchange and routing key
    /// </summary>
    /// <param name="exchange">The name of the exchange to which the objects will be forwarded. Cannot be <see langword="null"/>.</param>
    /// <param name="routingKey">The routing key used to route the forwarded objects. Cannot be <see langword="null"/>.</param>
    /// <param name="mandatory">A value indicating whether the forwarding operation is mandatory. If <see langword="true"/>, the operation
    /// requires confirmation that the message was routed successfully.</param>
    /// <param name="objectsToForward">The objects to be forwarded. Cannot be <see langword="null"/> and must contain at least one object.</param>
    public static ForwardResult<T> Forward<T>(string exchange, string routingKey, bool mandatory, params T[] objectsToForward)
        => Forward(exchange, routingKey, mandatory, (Action<T, IBasicProperties>)null, objectsToForward);

    /// <summary>
    /// Return a ForwardResult to represents a AMQP Forward to another exchange and routing key
    /// </summary>
    /// <param name="exchange">The name of the exchange to which the objects will be forwarded. Cannot be <see langword="null"/>.</param>
    /// <param name="routingKey">The routing key used to route the forwarded objects. Cannot be <see langword="null"/>.</param>
    /// <param name="mandatory">A value indicating whether the forwarding operation is mandatory. If <see langword="true"/>, the operation
    /// requires confirmation that the message was routed successfully.</param>
    /// <param name="replyTo">An optional reply-to address for responses. Can be <see langword="null"/> if no reply-to address is specified.</param>
    /// <param name="objectsToForward">The objects to be forwarded. Cannot be <see langword="null"/> and must contain at least one object.</param>
    public static ForwardResult<T> Forward<T>(string exchange, string routingKey, bool mandatory, string replyTo, params T[] objectsToForward)
        => Forward(exchange, routingKey, mandatory, (_, basicProperties) => basicProperties.ReplyTo = replyTo, objectsToForward);


    /// <summary>
    /// Return a ForwardResult to represents a AMQP Forward to another exchange and routing key
    /// </summary>
    /// <param name="exchange">The name of the exchange to which the objects will be forwarded. Cannot be <see langword="null"/>.</param>
    /// <param name="routingKey">The routing key used to route the forwarded objects. Cannot be <see langword="null"/>.</param>
    /// <param name="mandatory">A value indicating whether the forwarding operation is mandatory. If <see langword="true"/>, the operation
    /// requires confirmation that the message was routed successfully.</param>
    /// <param name="objectsToForward">The objects to be forwarded. Cannot be <see langword="null"/> and must contain at least one object.</param>

    public static ComposableResult ForwardAndAck<T>(string exchange, string routingKey, bool mandatory, params T[] objectsToForward) => new(Forward(exchange, routingKey, mandatory, (Action<T, IBasicProperties>)null, objectsToForward), s_forSuccess);

    /// <summary>
    /// Return a result that republishes the current message to the tail of the current queue.
    /// </summary>
    /// <returns></returns>
    public static RequeueToTailResult RequeueToTail() => RequeueToTail(options => { });

    /// <summary>
    /// Return a result that republishes the current message to the tail of the current queue.
    /// </summary>
    /// <param name="configure">Options configure action.</param>
    /// <returns></returns>
    public static RequeueToTailResult RequeueToTail(Action<RequeueToTailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new RequeueToTailOptions();
        configure(options);
        return new(options);
    }

    /// <summary>
    /// Return a result that republishes the current message to the tail of the specified queue.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <returns></returns>
    public static RequeueToTailResult RequeueToTail(string queueName) => RequeueToTail(queueName, options => { });

    /// <summary>
    /// Return a result that republishes the current message to the tail of the specified queue.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="configure">Options configure action.</param>
    /// <returns></returns>
    public static RequeueToTailResult RequeueToTail(string queueName, Action<RequeueToTailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new RequeueToTailOptions { QueueName = queueName };
        configure(options);
        return new(options);
    }


    /// <summary>
    /// Return a ComposableResult to execute multiple steps
    /// </summary>
    /// <param name="results"></param>
    /// <returns></returns>
    public static ComposableResult Compose(params IAmqpResult[] results) => new(results);


}
