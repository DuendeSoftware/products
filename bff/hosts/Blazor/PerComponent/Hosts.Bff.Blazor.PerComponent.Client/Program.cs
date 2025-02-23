using Duende.Bff.Blazor.Client;
using Hosts.Bff.Blazor.PerComponent.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<IRenderModeContext, ClientRenderModeContext>();

builder.Services
    .AddBffBlazorClient(opt =>
    {
        opt.RemoteApiPath = "/remote-apis";
    })
    .AddCascadingAuthenticationState()
    .AddRemoteApiHttpClient("callApi");

await builder.Build().RunAsync();
