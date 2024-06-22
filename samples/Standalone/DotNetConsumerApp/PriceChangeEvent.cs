// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

public record PriceChangeEvent
{
    public string CurrencyId { get; set; } //BRL
    public decimal CurrencyValue { get; set; } //5.43
    public string ReferenceCurrencyId { get; set; }  //USD  
}
