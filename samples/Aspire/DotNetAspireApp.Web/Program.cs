using DotNetAspireApp.Web;
using DotNetAspireApp.Web.Components;
using Radzen;

var builder = WebApplication.CreateBuilder(args);


// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.AddRedisOutputCache("redis");

// Add services to the container.
builder.Services
    .AddRadzenComponents()
    .AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddHttpClient<BackendApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
