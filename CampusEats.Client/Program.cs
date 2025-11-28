using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CampusEats.Client;
using CampusEats.Client.Services;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API HttpClient from appsettings.json
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
    ?? throw new InvalidOperationException("API BaseUrl not configured in appsettings.json");

// Add Blazored LocalStorage (must be before HttpClient registration)
builder.Services.AddBlazoredLocalStorage();

// Register auth token handler
builder.Services.AddScoped<AuthTokenHandler>();

// Configure HttpClient with auth handler
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthTokenHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

// Register our services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<DietaryTagService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<KitchenService>();
builder.Services.AddSingleton<CartService>();

await builder.Build().RunAsync();
