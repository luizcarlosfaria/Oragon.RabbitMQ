// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.


// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.ResultHandlers;

/// <summary>
/// Interface to handle a task
/// </summary>
public interface IResultHandler
{
    /// <summary>
    /// Handle a task
    /// </summary>
    /// <param name="context"></param>
    /// <param name="dispatchResult"></param>
    /// <returns></returns>
    Task<IAmqpResult> Handle(IAmqpContext context, object dispatchResult);

}
