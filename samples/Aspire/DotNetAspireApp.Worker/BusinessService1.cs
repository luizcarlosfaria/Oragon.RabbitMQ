// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using DotNetAspireApp.Common.Messages.Commands;

namespace DotNetAspireApp.Worker;

public class BusinessService1
{
    public async Task DoSomethingAsync(DoSomethingCommand command)
    {
        Console.WriteLine($"Consumer Recebeu | {command.ItemId}");

        await Task.Delay(5000);
    }
}
