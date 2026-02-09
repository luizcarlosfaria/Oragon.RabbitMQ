// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;

/// <summary>
/// Handles the result of a dispatched handler that returns a non-Task, non-void type.
/// If the result implements IAmqpResult, it is returned directly; otherwise, an Ack is returned.
/// </summary>
public class GenericResultHandler : VoidResultHandler
{
}
