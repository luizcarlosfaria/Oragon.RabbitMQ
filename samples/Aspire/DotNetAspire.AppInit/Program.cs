// See https://aka.ms/new-console-template for more information

using DotNetAspire.AppInit;
using DotNetAspire.Architecture.Aspire;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

builder.Services.AddHostedService<InitializerService>();

var app = builder.Build();

app.Run();

Console.WriteLine("Init");
