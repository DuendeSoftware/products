// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.TestIsolation;

/// <summary>
/// Per-test, per-server handle that exposes an <see cref="HttpClient"/>,
/// the per-test <see cref="IServiceProvider"/>, and auto-cleanup on dispose.
/// Each <see cref="TestScope"/> is bound to a specific server name at construction.
/// </summary>
public sealed class TestScope : IDisposable
{
    private readonly TestIsolationService _service;
    private readonly int _port;
    private readonly string _scheme;
    private readonly string _serverName;
    private readonly Lazy<HttpClient> _lazyClient;
    private bool _disposed;

    /// <summary>
    /// The numeric test ID associated with this scope.
    /// </summary>
    public int TestId { get; }

    /// <summary>
    /// The server name this scope is bound to (empty string for the default server).
    /// </summary>
    public string ServerName => _serverName;

    /// <summary>
    /// The per-test <see cref="IServiceProvider"/> for this server.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// The base address for this server.
    /// <para>
    /// Format: <c>{scheme}://{testId}-{serverName}.dev.localhost:{port}/</c>
    /// or <c>{scheme}://{testId}.dev.localhost:{port}/</c> when the server name is empty.
    /// </para>
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Pre-built <see cref="HttpClient"/> targeting this server.
    /// Lazily created on first access.
    /// </summary>
    public HttpClient Client => _lazyClient.Value;

    public TestScope(
        int testId,
        string serverName,
        int port,
        IServiceProvider services,
        TestIsolationService service,
        string scheme = "https")
    {
        TestId = testId;
        _serverName = serverName;
        _port = port;
        Services = services;
        _service = service;
        _scheme = scheme;

        BaseAddress = string.IsNullOrEmpty(serverName)
            ? new Uri($"{_scheme}://{TestId}.dev.localhost:{_port}/")
            : new Uri($"{_scheme}://{TestId}-{serverName}.dev.localhost:{_port}/");

        _lazyClient = new Lazy<HttpClient>(() => CreateClient());
    }

    /// <summary>
    /// Convenience constructor for the default (unnamed) server.
    /// </summary>
    public TestScope(
        int testId,
        int port,
        IServiceProvider services,
        TestIsolationService service,
        string scheme = "https")
        : this(testId, "", port, services, service, scheme)
    {
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> targeting this server.
    /// Each call creates a fresh <see cref="HttpClientHandler"/> to avoid cross-test
    /// cookie contamination. The caller owns the returned client.
    /// </summary>
    public HttpClient CreateClient(bool allowAutoRedirect = false)
    {
#pragma warning disable CA2000 // Ownership transferred to HttpClient via disposeHandler: true
        var handler = TestIsolationService.CreateHandler(allowAutoRedirect);
#pragma warning restore CA2000
#pragma warning disable CA5400 // CRL check intentionally disabled for test infrastructure
        return new HttpClient(handler, disposeHandler: true)
#pragma warning restore CA5400
        {
            BaseAddress = BaseAddress
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_lazyClient.IsValueCreated)
        {
            _lazyClient.Value.Dispose();
        }
        _service.UnregisterTest(TestId);
    }
}
