// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Interface to handle a task
/// </summary>
public interface IResultHandler
{
    /// <summary>
    /// Handle a task
    /// </summary>
    /// <param name="dispatchResult"></param>
    /// <returns></returns>
    Task<IAMQPResult> Handle(object dispatchResult);

}
