using Duende.Bff.Blazor.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services
    .AddBffBlazorClient() // Provides auth state provider that polls the /bff/user endpoint
    .AddCascadingAuthenticationState();

builder.Services.AddSingleton<IWeatherClient>(sp => sp.GetRequiredService<WeatherClient>());

builder.Services.AddLocalApiHttpClient<WeatherClient>();

await builder.Build().RunAsync();
