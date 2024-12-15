// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// This interface is responsible for consuming messages from RabbitMQ.
/// </summary>
public interface IHostedAmqpConsumer : IHostedService, IAsyncDisposable;
