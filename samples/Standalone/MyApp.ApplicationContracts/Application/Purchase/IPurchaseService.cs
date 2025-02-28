// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace MyApp.Application.Purchase;

public interface IPurchaseService
{
    void Purchase(PurchaseRequest request);

    Task PurchaseAsync(PurchaseRequest request);
}
