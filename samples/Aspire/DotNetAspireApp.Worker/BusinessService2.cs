// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using DotNetAspireApp.Common.Messages.Commands;

namespace DotNetAspireApp.Worker;

public class BusinessService2
{
    public async Task<DoSomethingCommand> DoSomething2Async(DoSomethingCommand command)
    {
        Console.WriteLine($"Consumer Recebeu | {command.Text}");

        await Task.Delay(5000).ConfigureAwait(false);

        return command;
    }
}
