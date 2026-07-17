// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.DynamicQueues;

/// <summary>
/// Start hook decision.
/// </summary>
public sealed record DynamicQueueStartDecision(
    DynamicQueueStartDecisionType Type,
    TimeSpan? SuggestedDelay = null,
    Exception Exception = null)
{
    /// <summary>
    /// Creates an allow decision.
    /// </summary>
    public static DynamicQueueStartDecision Allow() => new(DynamicQueueStartDecisionType.Allow);

    /// <summary>
    /// Creates a skip decision.
    /// </summary>
    public static DynamicQueueStartDecision Skip() => new(DynamicQueueStartDecisionType.Skip);

    /// <summary>
    /// Creates a defer decision.
    /// </summary>
    /// <param name="suggestedDelay">Optional suggested delay.</param>
    public static DynamicQueueStartDecision Defer(TimeSpan? suggestedDelay = null) =>
        new(DynamicQueueStartDecisionType.Defer, suggestedDelay);

    /// <summary>
    /// Creates a fail decision.
    /// </summary>
    /// <param name="exception">Failure exception.</param>
    public static DynamicQueueStartDecision Fail(Exception exception) =>
        new(DynamicQueueStartDecisionType.Fail, Exception: exception);
}
