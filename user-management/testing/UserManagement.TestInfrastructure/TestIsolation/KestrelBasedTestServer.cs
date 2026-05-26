// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.TestIsolation;

/// <summary>
/// A lightweight wrapper around <see cref="TestIsolationService"/> that provides a
/// WebApplication-like API for registering an isolated test server.
/// <para>
/// The test ID is allocated at construction so <see cref="BaseAddress"/> and
/// <see cref="BuildUrl"/> are available before <see cref="StartAsync"/> is called.
/// This allows other servers in the same test to reference this server's URLs
/// during their own configuration.
/// </para>
/// </summary>
public sealed class KestrelBasedTestServer : IAsyncDisposable
{
    private readonly string _serverName;
    private readonly Action<IServiceCollection> _configureServices;
    private readonly Action<WebAppWrapper> _configurePipeline;
    private readonly TestIsolationService _isolationService;
    private readonly ITestOutputHelper _output;
    private readonly int _port;

    private TestScope? _scope;
    private HttpClient? _defaultClient;

    /// <summary>
    /// The numeric test ID uniquely identifying this server instance.
    /// Allocated at construction so <see cref="BaseAddress"/> is available immediately.
    /// </summary>
    public int TestId { get; }

    /// <summary>
    /// The base address for this server.
    /// Available immediately — does not require <see cref="StartAsync"/> to be called first.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// The per-test <see cref="IServiceProvider"/>. Only available after <see cref="StartAsync"/>.
    /// </summary>
    public IServiceProvider Services => _scope?.Services
        ?? throw new InvalidOperationException("Server has not been started. Call StartAsync first.");

    /// <summary>
    /// Pre-built <see cref="HttpClient"/> targeting this server with HTTP logging enabled.
    /// Only available after <see cref="StartAsync"/>.
    /// </summary>
    public HttpClient Client
    {
        get
        {
            if (_scope is null)
            {
                throw new InvalidOperationException("Server has not been started. Call StartAsync first.");
            }
            return _defaultClient ??= CreateClient();
        }
    }

    public KestrelBasedTestServer(
        string serverName,
        ITestServerFixture fixture,
        ITestOutputHelper output,
        Action<IServiceCollection> configureServices,
        Action<WebAppWrapper> configurePipeline)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(configureServices);
        ArgumentNullException.ThrowIfNull(configurePipeline);

        TestId = TestIsolationService.AllocateTestId();
        _serverName = serverName;
        _port = fixture.Port;
        _isolationService = fixture.IsolationService;
        _output = output;

        _configureServices = configureServices;
        _configurePipeline = configurePipeline;

        BaseAddress = string.IsNullOrEmpty(serverName)
            ? new Uri($"https://{TestId}.dev.localhost:{_port}/")
            : new Uri($"https://{TestId}-{serverName}.dev.localhost:{_port}/");
    }

    /// <summary>
    /// Registers a hostname alias so that requests arriving with the given hostname
    /// are dispatched to this server's pipeline. Useful for multi-space scenarios
    /// where space origins differ from the server's canonical hostname.
    /// Only available after <see cref="StartAsync"/>.
    /// </summary>
    public void RegisterHostAlias(string hostname)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostname);

        if (_scope is null)
        {
            throw new InvalidOperationException("Server has not been started. Call StartAsync first.");
        }

        _isolationService.RegisterHostAlias(hostname, TestId, _serverName);
    }

    /// <summary>
    /// Builds an absolute URL by appending <paramref name="path"/> to <see cref="BaseAddress"/>.
    /// Available before <see cref="StartAsync"/> is called.
    /// </summary>
    public Uri BuildUrl(string path) => new(BaseAddress, path);

    /// <summary>
    /// Registers the server with the isolation service, building the DI container
    /// and pipeline. After this call, <see cref="Services"/> and <see cref="Client"/>
    /// become available.
    /// </summary>
    public Task StartAsync()
    {
        if (_scope is not null)
        {
            throw new InvalidOperationException("Server has already been started.");
        }

        var services = _isolationService.RegisterServer(
            TestId, _serverName, _configureServices, _configurePipeline);

        _scope = new TestScope(TestId, _serverName, _port, services, _isolationService);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> targeting this server with HTTP logging enabled.
    /// Only available after <see cref="StartAsync"/>.
    /// </summary>
    public HttpClient CreateClient(bool allowAutoRedirect = false)
    {
        if (_scope is null)
        {
            throw new InvalidOperationException("Server has not been started. Call StartAsync first.");
        }

#pragma warning disable CA2000 // Ownership transferred to HttpClient via disposeHandler: true
        var handler = CreateHandler(allowAutoRedirect);
#pragma warning restore CA2000
#pragma warning disable CA5400 // CRL check intentionally disabled for test infrastructure
        return new HttpClient(handler, disposeHandler: true)
#pragma warning restore CA5400
        {
            BaseAddress = BaseAddress
        };
    }

    /// <summary>
    /// Creates a new <see cref="LoggingHandler"/> wrapping an <see cref="HttpClientHandler"/>
    /// that trusts the dev certificate. HTTP traffic is logged to the test output.
    /// </summary>
    public LoggingHandler CreateHandler(bool allowAutoRedirect = false) =>
#pragma warning disable CA2000
        new(TestIsolationService.CreateHandler(allowAutoRedirect), _output, _serverName);
#pragma warning restore CA2000

    /// <summary>
    /// Resolves a service of type <typeparamref name="T"/> from the per-test DI container.
    /// Only available after <see cref="StartAsync"/>.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull =>
        Services.GetRequiredService<T>();

    public ValueTask DisposeAsync()
    {
        _defaultClient?.Dispose();
        _defaultClient = null;
        _scope?.Dispose();
        _scope = null;
        return ValueTask.CompletedTask;
    }
}
