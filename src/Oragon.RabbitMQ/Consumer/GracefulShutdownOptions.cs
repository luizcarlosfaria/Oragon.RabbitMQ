// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// Options for graceful consumer shutdown.
/// </summary>
public sealed class GracefulShutdownOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the context cancellation token is canceled on stop.
    /// </summary>
    public bool CancelContextTokenOnStop { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether in-flight messages are awaited on stop.
    /// </summary>
    public bool WaitForInFlightMessages { get; set; }

    /// <summary>
    /// Gets or sets the maximum time to wait for in-flight messages.
    /// </summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
