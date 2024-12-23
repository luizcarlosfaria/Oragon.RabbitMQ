// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;

namespace Oragon.RabbitMQ.Consumer.Dispatch;

public class DynamicArgumentBinder : IAmqpArgumentBinder
{
    private readonly Func<IAmqpContext, object> func;

    public DynamicArgumentBinder(Func<IAmqpContext, object> func)
    {
        this.func = func;
    }

    public object GetValue(IAmqpContext context)
    {
        _ = Guard.Argument(context).NotNull();

        var result = this.func(context);

        return result;
    }
}
