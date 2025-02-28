// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Purchase;

namespace MyApp;

public static partial class Configure
{


    public static void ConfigureHttp(this WebApplicationBuilder builder)
    {
        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        _ = builder.Services.AddOpenApi();
    }

    public static void ConfigureHttp(this WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            _ = app.MapOpenApi();
        }

        _ = app.UseHttpsRedirection();

        app.ConfigureHttpEndpoints();
    }

    private static void ConfigureHttpEndpoints(this WebApplication app)
    {
        _ = app.MapGet("/hello-world", () =>
        {
            return new HelloWorld($"Hello World Oragon.RabbitMQ | {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        })
        .WithName("GetWeatherForecast");

        _ = app.MapPost(
            "/purchase",
                ([FromServices] IPurchaseService svc, [FromBody] PurchaseRequest request)
                => svc.Purchase(request)
            )
        .WithName("Purchase");
    }


}
