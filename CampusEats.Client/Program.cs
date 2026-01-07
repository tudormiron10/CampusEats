using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CampusEats.Client;
using CampusEats.Client.Services;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API HttpClient from appsettings.json
// Empty or missing BaseUrl means same-origin deployment (use host origin)
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];

// Add Blazored LocalStorage (must be before HttpClient registration)
builder.Services.AddBlazoredLocalStorage();

// Register auth token handler
builder.Services.AddScoped<AuthTokenHandler>();

// Configure HttpClient with auth handler
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthTokenHandler>();
    handler.InnerHandler = new HttpClientHandler();

    // Use configured BaseUrl or fall back to current host origin (same-origin deployment)
    var baseAddress = string.IsNullOrEmpty(apiBaseUrl)
        ? new Uri(builder.HostEnvironment.BaseAddress)
        : new Uri(apiBaseUrl);

    return new HttpClient(handler) { BaseAddress = baseAddress };
});

// Register our services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<DietaryTagService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<KitchenService>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<CartService>();

// Singleton: one SignalR connection shared across all pages
// Compute hub URL at registration time (same-origin deployment when BaseUrl is empty)
var hubBaseUrl = string.IsNullOrEmpty(apiBaseUrl)
    ? builder.HostEnvironment.BaseAddress.TrimEnd('/')
    : apiBaseUrl.TrimEnd('/');
builder.Services.AddSingleton(new OrderHubService($"{hubBaseUrl}/api/hubs/orders"));

await builder.Build().RunAsync();
