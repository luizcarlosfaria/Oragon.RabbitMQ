// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

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
    /// Return a ReplyResult to represents a AMQP Reply
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="objectToReturn"></param>
    /// <returns></returns>
    public static ReplyResult<T> Reply<T>(T objectToReturn) => new(objectToReturn);

    /// <summary>
    /// Return a ComposableResult to execute multiple steps
    /// </summary>
    /// <param name="results"></param>
    /// <returns></returns>
    public static ComposableResult Compose(params IAmqpResult[] results) => new(results);


}
