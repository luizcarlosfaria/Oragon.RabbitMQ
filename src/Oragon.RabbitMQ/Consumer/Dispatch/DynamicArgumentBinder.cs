// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

/// <summary>
/// Represents a dynamic argument binder for an AMQP message.
/// </summary>
public class DynamicArgumentBinder : IAmqpArgumentBinder
{
    private readonly Func<IAmqpContext, object> func;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicArgumentBinder"/> class with the specified function.
    /// </summary>
    /// <param name="func"></param>
    public DynamicArgumentBinder(Func<IAmqpContext, object> func)
    {
        this.func = func;
    }

    /// <summary>
    /// Gets the value of the argument.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public object GetValue(IAmqpContext context)
    {
        _ = Guard.Argument(context).NotNull();

        var result = this.func(context);

        return result;
    }
}
