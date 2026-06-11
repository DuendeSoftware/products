// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;

/// <summary>
/// Central registry for isolated pipeline and DI registrations.
/// Registrations are keyed by <c>(testId, serverName)</c>. Each <see cref="KestrelBasedTestServer"/>
/// allocates its own unique test ID, so the test ID is per-server-instance, not shared.
/// Creates per-test <see cref="HttpClientHandler"/> instances to avoid cross-test cookie contamination.
/// </summary>
public sealed class TestIsolationService : IDisposable
{
    private static int _nextTestId;

    private readonly ConcurrentDictionary<(int TestId, string ServerName), TestRegistration> _tests = new();
    private readonly ConcurrentDictionary<string, (int TestId, string ServerName)> _hostAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ServiceDescriptor> _baseServices = [];
    private readonly Lock _registerLock = new();

    private IApplicationBuilder? _rootBuilder;

    /// <summary>
    /// The global <see cref="IServiceProvider"/> from the shared host.
    /// Set by <see cref="SetApplicationBuilder"/>.
    /// </summary>
    public IServiceProvider GlobalServices { get; private set; } = null!;

    /// <summary>
    /// Allocates a new unique test ID. Thread-safe via <see cref="Interlocked.Increment(ref int)"/>.
    /// </summary>
    internal static int AllocateTestId() => Interlocked.Increment(ref _nextTestId);

    /// <summary>
    /// Snapshots the current service collection so each test can clone and override.
    /// Must be called after all base service registrations.
    /// </summary>
    public void SetBaseServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _baseServices.Clear();
        foreach (var descriptor in services)
        {
            _baseServices.Add(descriptor);
        }
    }

    /// <summary>
    /// Captures the root <see cref="IApplicationBuilder"/> for pipeline branching.
    /// Called by <see cref="TestIsolationExtensions.UseTestIsolation"/>.
    /// </summary>
    public void SetApplicationBuilder(IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _rootBuilder = builder;
        GlobalServices = builder.ApplicationServices;
    }

    /// <summary>
    /// Registers a named server for the given test ID.
    /// Clones base services, applies per-test overrides, builds a branch pipeline,
    /// and stores the registration. Returns the per-test <see cref="IServiceProvider"/>.
    /// </summary>
    public IServiceProvider RegisterServer(
        int testId,
        string serverName,
        Action<IServiceCollection> configureServices,
        Action<WebAppWrapper> configurePipeline)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        ArgumentNullException.ThrowIfNull(configurePipeline);
        ArgumentNullException.ThrowIfNull(serverName);

        // Normalize for case-insensitive hostname matching.
        serverName = serverName.ToUpperInvariant();

        if (_rootBuilder is null)
        {
            throw new InvalidOperationException(
                "SetApplicationBuilder must be called before RegisterServer. " +
                "Ensure UseTestIsolation() was called on the WebApplication.");
        }

        // Serialize pipeline construction. UseRouting()/UseEndpoints() use
        // ObservableCollection<EndpointDataSource> internally which is NOT
        // thread-safe. Concurrent RegisterServer calls corrupt the collection
        // causing ArgumentOutOfRangeException / IndexOutOfRangeException.
        // The lock is only held during one-time pipeline build — the hot
        // path (request dispatch) is lock-free.
        lock (_registerLock)
        {
            // 1. Clone base services and apply per-test overrides
            var testServices = new ServiceCollection();
            foreach (var descriptor in _baseServices)
            {
                ((IList<ServiceDescriptor>)testServices).Add(descriptor);
            }
            configureServices(testServices);

            // Replace the EndpointDataSource singleton with a fresh instance per test.
            // AddRouting() captures an ObservableCollection<EndpointDataSource> by closure
            // in the factory lambda. When base services are cloned, that closure is shared
            // across all test containers, causing data sources from disposed tests to leak
            // into subsequent tests (ObjectDisposedException). Replacing the registration
            // ensures each test gets an isolated endpoint data source collection.
            _ = testServices.RemoveAll<EndpointDataSource>();
            var perTestDataSources = new ObservableCollection<EndpointDataSource>();
            _ = testServices.AddSingleton<EndpointDataSource>(
                _ => new CompositeEndpointDataSource(perTestDataSources));

            var testProvider = testServices.BuildServiceProvider();

            // 2. Create a standalone pipeline builder for this test.
            //    We intentionally avoid _rootBuilder.New() because it creates a
            //    CopyOnWriteDictionary that shares Properties with the root builder,
            //    which leaks state (endpoint data sources, service provider references)
            //    between tests and causes ObjectDisposedException when a prior test's
            //    IServiceProvider is disposed.
            var branchBuilder = new ApplicationBuilder(testProvider, _rootBuilder.ServerFeatures);

            var wrapper = new WebAppWrapper(branchBuilder, testProvider);

            _ = wrapper.UseAuthentication();
            configurePipeline(wrapper);
            wrapper.FinalizeEndpoints();
            var pipeline = branchBuilder.Build();

            // 3. Store registration (thread-safe)
            var key = (testId, serverName);
            var registration = new TestRegistration(pipeline, testProvider);
            if (!_tests.TryAdd(key, registration))
            {
                registration.Dispose();
                throw new InvalidOperationException(
                    $"Server '{serverName}' for test {testId} is already registered.");
            }

            return testProvider;
        }
    }

    /// <summary>
    /// Registers a single unnamed server for the given test ID.
    /// Convenience wrapper over <see cref="RegisterServer"/>.
    /// </summary>
    public IServiceProvider RegisterTest(
        int testId,
        Action<IServiceCollection> configureServices,
        Action<WebAppWrapper> configurePipeline) =>
        RegisterServer(testId, "", configureServices, configurePipeline);

    /// <summary>
    /// Looks up the registration for the given test ID and server name.
    /// Returns <c>false</c> if not found (request should fall through).
    /// </summary>
    public bool TryGetRegistration(int testId, string serverName, out TestRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(serverName);
        return _tests.TryGetValue((testId, serverName.ToUpperInvariant()), out registration);
    }

    /// <summary>
    /// Registers a hostname alias that maps to an existing server registration.
    /// Requests arriving with this hostname (without port) will be dispatched to
    /// the target server's pipeline. This enables multi-space scenarios where
    /// space origins (e.g. <c>space1.dev.localhost</c>) must route to a shared gateway.
    /// </summary>
    public void RegisterHostAlias(string hostname, int testId, string serverName)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostname);
        ArgumentNullException.ThrowIfNull(serverName);

        if (!_hostAliases.TryAdd(hostname, (testId, serverName.ToUpperInvariant())))
        {
            throw new InvalidOperationException(
                $"Host alias '{hostname}' is already registered.");
        }
    }

    /// <summary>
    /// Looks up a registration by raw hostname (without port).
    /// Checks host aliases registered via <see cref="RegisterHostAlias"/>.
    /// Returns <c>false</c> if no alias matches or the target registration is missing.
    /// </summary>
    public bool TryGetRegistrationByHost(string hostname, out TestRegistration? registration)
    {
        registration = null;
        return _hostAliases.TryGetValue(hostname, out var key)
            && _tests.TryGetValue(key, out registration);
    }

    /// <summary>
    /// Removes and disposes all registrations (servers and host aliases) for the given test ID.
    /// Since each <see cref="KestrelBasedTestServer"/> allocates a unique test ID, this
    /// effectively cleans up a single server instance.
    /// Called automatically when a <see cref="TestScope"/> is disposed.
    /// </summary>
    public void UnregisterTest(int testId)
    {
        foreach (var alias in _hostAliases.Where(kv => kv.Value.TestId == testId).ToList())
        {
            _ = _hostAliases.TryRemove(alias.Key, out _);
        }

        foreach (var key in _tests.Keys.Where(k => k.TestId == testId).ToList())
        {
            if (_tests.TryRemove(key, out var registration))
            {
                registration.Dispose();
            }
        }
    }

    /// <summary>
    /// Creates a plain <see cref="HttpClientHandler"/> suitable for use in tests.
    /// Each call creates a fresh handler to avoid cross-test cookie contamination.
    /// The caller owns the returned handler and must dispose it.
    /// </summary>
    public static HttpClientHandler CreateHandler(bool allowAutoRedirect = false) =>
        new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

    public void Dispose()
    {
        foreach (var registration in _tests.Values)
        {
            registration.Dispose();
        }
        _tests.Clear();
    }
}
