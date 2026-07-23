// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Duende.IdentityServer.UI.Infra;

public abstract class TestHost(
    IScenarioConfigurator configurator,
    string name)
    : IAsyncDisposable
{
    private WebApplicationBuilder? _builder = configurator.CreateBuilder(name);

    public string Name => name;

    public IServiceCollection Services => _builder == null
        ? throw new InvalidOperationException("Already created app")
        : _builder.Services;

    public WebApplication App
    {
        get
        {
            if (field == null)
            {
                field = CreateApp(_builder ?? throw new InvalidOperationException("Builder cannot be null"));
                _builder = null;
            }

            return field;
        }
    } = null;

    protected abstract WebApplication CreateApp(WebApplicationBuilder builder);

    public async Task StartAsync(Ct ct) => await App.StartAsync(ct);

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    public Uri BuildUri(string? path = null)
    {
        var server = App.Services.GetRequiredService<IServer>();
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        var assignedAddress = addressesFeature?.Addresses.FirstOrDefault() ?? throw new InvalidOperationException("Can't find server address");
        var serverUri = new Uri(assignedAddress);
        return new Uri(new Uri($"https://{name}.dev.localhost:{serverUri.Port}"), path);
    }

    public ScenarioLink Link => new ScenarioLink(name, BuildUri());
}
