// See https://aka.ms/new-console-template for more information

using AspireApp1.AppInit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

builder.Services.AddHostedService<InitializerService>();

var app = builder.Build();

app.Run();

Console.WriteLine("Init");
