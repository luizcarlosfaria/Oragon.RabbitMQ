// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.


namespace MyApp.Application.Purchase;

public class PurchaseService : IPurchaseService
{
    public void Purchase(PurchaseRequest request)
    {
        if (request.Products?.Any(it => it.ProductId == 99) ?? false)
            throw new InvalidOperationException($"An data error on database. The product 99 is misconfigured. Product without price.");


        // Purchase logic     
    }

    public async Task PurchaseAsync(PurchaseRequest request)
    {
        if (request.Products?.Any(it => it.ProductId == 99) ?? false)
            throw new InvalidOperationException($"An data error on database. The product 99 is misconfigured. Product without price.");


        // Purchase logic
        //

        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
    }
}
