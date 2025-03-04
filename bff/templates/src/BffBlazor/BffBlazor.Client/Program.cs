using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Duende.Bff.Blazor.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services
    .AddBffBlazorClient() // Provides auth state provider that polls the /bff/user endpoint
    .AddCascadingAuthenticationState();

builder.Services.AddLocalApiHttpClient<WeatherHttpClient>();

await builder.Build().RunAsync();