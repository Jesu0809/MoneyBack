using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MoneyBack.Web;
using MoneyBack.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Singleton (no Scoped): IHttpClientFactory construye los DelegatingHandler en su
// propio scope de DI, separado del resto de la app. Con Scoped, el handler vería
// una instancia de TokenStore distinta a la que Login/AuthService actualizan, y el
// access token nunca llegaría a las requests (bug real, visto en pruebas).
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<UiOverlayService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<AuthorizedHttpMessageHandler>();

builder.Services.AddHttpClient("ApiAnon", client => client.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddHttpClient("Api", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizedHttpMessageHandler>();

var host = builder.Build();

var authService = host.Services.GetRequiredService<AuthService>();
await authService.InicializarAsync();

var themeService = host.Services.GetRequiredService<ThemeService>();
await themeService.InicializarAsync();

await host.RunAsync();
