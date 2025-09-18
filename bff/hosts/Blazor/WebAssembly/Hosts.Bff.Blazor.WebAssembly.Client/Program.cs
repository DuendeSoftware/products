// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Blazor.Client;
using Hosts.Bff.Blazor.WebAssembly.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);


builder.Services
    .AddBffBlazorClient() // Provides auth state provider that polls the /bff/user endpoint
    .AddCascadingAuthenticationState();

builder.Services.AddLocalApiHttpClient<WeatherHttpClient>();

await builder.Build().RunAsync();
