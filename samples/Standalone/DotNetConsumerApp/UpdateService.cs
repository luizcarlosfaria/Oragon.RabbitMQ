// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

public class UpdateService
{
    public Task UpdatePriceCacheAsync(PriceChangeEvent priceChangeEvent)
    {

        Console.WriteLine(priceChangeEvent);

        if(priceChangeEvent.CurrencyValue == 7M) throw new Exception("Invalid value");


        return Task.CompletedTask;
    }
}

