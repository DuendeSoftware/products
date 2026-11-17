// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.OutsideIn.Harness.Internal;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// The <c>env</c> passed to the <c>TestEnvironment.StartAsync</c> setup lambda (design doc sections 05 and 09). The
/// system under test always exists; <see cref="SetupIdentityServer" /> only configures it. The lambda must return
/// the result of <see cref="SetupPerIssuer{TResources}" />, which is the only way to write a test (D37). Nothing
/// starts until the lambda returns.
/// </summary>
public sealed class TestEnvironmentBuilder
{
    private readonly IReadOnlyList<Issuer> _issuers;
    private readonly List<IdentityServerTopology> _remoteServers = [];
    private readonly List<ClientApp> _clientApps = [];
    private readonly SnapshotScrubber _scrubber;
    private readonly TestClock _clock;
    private Issuer? _currentIssuer;
    private bool _perIssuerSetUp;

    internal TestEnvironmentBuilder(IdentityServerTopology systemUnderTest, IReadOnlyList<Issuer> issuers, SnapshotScrubber scrubber, TestClock clock)
    {
        SystemUnderTest = systemUnderTest;
        _issuers = issuers;
        _scrubber = scrubber;
        _clock = clock;
    }

    internal IdentityServerTopology SystemUnderTest { get; }

    internal IReadOnlyList<IdentityServerTopology> RemoteServers => _remoteServers;

    internal IReadOnlyList<ClientApp> ClientApps => _clientApps;

    /// <summary>Configures the system under test. Optional: there is always one (D37).</summary>
    public void SetupIdentityServer(Action<IdentityServerTopologyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(SystemUnderTest.Builder);
    }

    /// <summary>
    /// Declares what each issuer needs (D35). <paramref name="setup" /> runs right away, once per issuer: once without
    /// Spaces, once per space with Spaces on. Whatever it returns (typically a named tuple) is handed back, per issuer,
    /// to <c>SetupData</c> and <c>VerifyScenario</c>, with the same type and tuple element names.
    /// </summary>
    public PerIssuer<TResources> SetupPerIssuer<TResources>(Func<IIssuer, TResources> setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (_perIssuerSetUp)
        {
            throw new InvalidOperationException("SetupPerIssuer can only be called once per environment.");
        }

        _perIssuerSetUp = true;
        var perIssuer = new List<(Issuer, TResources)>();
        foreach (var issuer in _issuers)
        {
            _currentIssuer = issuer;
            try
            {
                perIssuer.Add((issuer, setup(issuer)));
            }
            finally
            {
                _currentIssuer = null;
            }
        }

        return new PerIssuer<TResources>(perIssuer);
    }

    /// <summary>
    /// Adds an upstream IdentityServer, with 2 nodes behind its own load balancer like the system under test (D36).
    /// Called inside <see cref="SetupPerIssuer{TResources}" />, it belongs to the current issuer: its URL appears as
    /// <c>{name}</c> while that issuer's scenario runs, and as <c>{name@issuer}</c> for any other issuer. Always runs
    /// with no caching, whatever the permutation: it stands in for an upstream server, not the system under test.
    /// </summary>
    public IIdentityServer AddRemoteIdentityServer(string name, Action<IdentityServerTopologyBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var server = new IdentityServerTopology(name, HostNameFor(name), owner: _currentIssuer?.Name, isSystemUnderTest: false, StoreType.InMemory, CachingMode.None, UserStore.TestUsers, spacesOn: false, _scrubber, _clock);
        configure?.Invoke(server.Builder);
        _remoteServers.Add(server);
        return server;
    }

    /// <summary>
    /// Adds a client web app (design doc section 05). Called inside <see cref="SetupPerIssuer{TResources}" />, it belongs
    /// to the current issuer, like a remote server. Its URL appears as <c>{name}</c>, its client ID as
    /// <c>{client:name}</c> and its secret as <c>{secret:name}</c>.
    /// </summary>
    public IWebApp AddClientApp(string name, Action<ClientAppBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ClientAppBuilder();
        configure(builder);
        var app = new ClientApp(name, HostNameFor(name), builder);
        var owner = _currentIssuer?.Name;
        _scrubber.Register(app.BaseUrl.GetLeftPart(UriPartial.Authority), name, owner);
        _scrubber.Register(app.ClientId, $"client:{name}", owner);
        _scrubber.Register(app.ClientSecret, $"secret:{name}", owner);
        _clientApps.Add(app);
        return app;
    }

    // With several issuers, each one gets its own server or app with the same name, on its own host.
    private string HostNameFor(string name)
    {
        var hostName = _currentIssuer is not null && _issuers.Count > 1 ? $"{name}.{_currentIssuer.Name}" : name;
        if (hostName == SystemUnderTest.HostName
            || _remoteServers.Any(server => server.HostName == hostName)
            || _clientApps.Any(app => app.HostName == hostName))
        {
            throw new InvalidOperationException($"A server or app with host name '{hostName}' already exists in this environment.");
        }

        return hostName;
    }
}

/// <summary>
/// What <see cref="TestEnvironmentBuilder.SetupPerIssuer{TResources}" /> returns, and the setup lambda must return in
/// turn. Carries each issuer's resources, typed as the per-issuer lambda's return type, into the environment.
/// </summary>
public sealed class PerIssuer<TResources>
{
    internal PerIssuer(IReadOnlyList<(Issuer Issuer, TResources Resources)> issuers) => Issuers = issuers;

    internal IReadOnlyList<(Issuer Issuer, TResources Resources)> Issuers { get; }
}
