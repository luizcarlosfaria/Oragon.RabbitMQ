// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Mvc;

/// <summary>
/// Minimal stand-in for the real ASP.NET Core MVC "FromBody" attribute. It exists only so
/// <c>ArgumentBinderDiscoveryErrorTests</c> can verify that
/// <c>Oragon.RabbitMQ.Consumer.Dispatch.ArgumentBinderExtensions</c> rejects handler parameters carrying
/// attributes from the "Microsoft.AspNetCore.Mvc" namespace, without taking a dependency on the real
/// ASP.NET Core MVC package. The match in production code is done by attribute type name and namespace
/// string, so this local declaration is sufficient to trigger the same code path.
/// </summary>
public sealed class FromBodyAttribute : Attribute
{
}
