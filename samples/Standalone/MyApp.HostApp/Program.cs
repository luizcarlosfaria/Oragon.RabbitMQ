// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using MyApp;
using MyApp.Application.Purchase;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureHttp();
builder.ConfigureAmqp();

builder.Services.AddScoped<IPurchaseService, PurchaseService>();

var app = builder.Build();


app.ConfigureHttp();
app.ConfigureAmqp();

app.Run();

internal record HelloWorld(string Message)
{
}
