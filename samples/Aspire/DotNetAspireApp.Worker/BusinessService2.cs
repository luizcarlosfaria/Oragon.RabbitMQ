// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace DotNetAspireApp.Worker;

public class BusinessService2
{
    public async Task<BusinessCommandOrEvent> DoSomething2Async(BusinessCommandOrEvent commandOrEvent)
    {
        Console.WriteLine($"Consumer Recebeu | {commandOrEvent.ItemId}");

        await Task.Delay(5000);

        return commandOrEvent;
    }
}
