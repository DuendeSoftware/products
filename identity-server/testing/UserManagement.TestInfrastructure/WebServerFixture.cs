// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement;

public sealed class WebServerFixture : IAsyncLifetime, ITestServerFixture
{
    private WebApplication _app = null!;
    private TestIsolationService _isolationService = null!;

    /// <summary>
    /// The port the server is listening on. Use <see cref="TestScope.BaseAddress"/>
    /// to construct per-test URLs.
    /// </summary>
    public int Port { get; private set; }

    public TestIsolationService IsolationService => _isolationService;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.Logging.ClearProviders();
        // Bind to loopback so the test server is not exposed outside the
        // local machine/CI job. Port 0 lets the OS assign a free port;
        // hostname-based routing still works via the Host header since
        // *.dev.localhost resolves to 127.0.0.1.
        _ = builder.WebHost.UseUrls("https://127.0.0.1:0");

        _ = builder.Services.AddHttpContextAccessor();
        // AddTestIsolation must be called LAST — it snapshots the service
        // collection so per-test containers include all base registrations.
        _ = builder.Services.AddTestIsolation();
        _app = builder.Build();
        _ = _app.UseTestIsolation();

        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>()!;
        var address = new Uri(addressFeature.Addresses.First());
        Port = address.Port;

        _isolationService = _app.Services.GetRequiredService<TestIsolationService>();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        _isolationService.Dispose();
        await _app.DisposeAsync();
    }
}
