namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Define scope used on dispatch
/// </summary>
public enum DispatchScope
{
    /// <summary>
    /// Ignore scope (will cause error)
    /// </summary>
    None,

    /// <summary>
    /// Use the same scope as the parent
    /// </summary>
    RootScope,

    /// <summary>
    /// Use a new scope for each message
    /// </summary>
    ChildScope
}
