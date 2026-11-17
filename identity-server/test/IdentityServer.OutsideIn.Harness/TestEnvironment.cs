// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// Non-generic entry point, so a test writes <c>await TestEnvironment.StartAsync(p, env => ...)</c> and the resources
/// type is inferred from what <c>SetupPerIssuer</c> returns.
/// </summary>
public static class TestEnvironment
{
    /// <summary>The system under test's name, and its placeholder in snapshots: <c>{sut}</c>.</summary>
    public const string SystemUnderTestName = "sut";

    /// <summary>
    /// Starts an environment for one <paramref name="permutation" />: the system under test (always one, with 2 nodes
    /// behind a load balancer, D36), plus whatever the setup lambda adds.
    /// </summary>
    public static Task<TestEnvironment<TResources>> StartAsync<TResources>(
        Permutation permutation,
        Func<TestEnvironmentBuilder, PerIssuer<TResources>> setup) =>
        TestEnvironment<TResources>.StartAsync(permutation, setup, TestContext.Current.CancellationToken);
}

/// <summary>
/// Root of a test setup (design doc sections 05 and 09). A test runs its scenario through it once for every issuer:
/// <see cref="SetupData" /> for every issuer first, then <see cref="VerifyScenario{TResult}" /> for each issuer in turn.
/// </summary>
/// <typeparam name="TResources">What the per-issuer setup lambda returned, for example a named tuple.</typeparam>
public sealed class TestEnvironment<TResources> : IAsyncDisposable
{
    private const string Space1Host = "space-1.sut";

    private readonly IdentityServerTopology _systemUnderTest;
    private readonly IReadOnlyList<IdentityServerTopology> _remoteServers;
    private readonly IReadOnlyList<ClientApp> _clientApps;
    private readonly IReadOnlyList<(Issuer Issuer, TResources Resources)> _issuers;
    private readonly VirtualHostRegistry _virtualHosts;
    private readonly SnapshotScrubber _scrubber;
    private readonly HarnessLog _log;
    private readonly List<TestBrowser> _browsers = [];
    private readonly List<HttpClient> _backChannelClients = [];

    private TestEnvironment(
        Permutation permutation,
        TestClock clock,
        IdentityServerTopology systemUnderTest,
        IReadOnlyList<IdentityServerTopology> remoteServers,
        IReadOnlyList<ClientApp> clientApps,
        IReadOnlyList<(Issuer Issuer, TResources Resources)> issuers,
        VirtualHostRegistry virtualHosts,
        SnapshotScrubber scrubber,
        HarnessLog log)
    {
        Permutation = permutation;
        Clock = clock;
        _systemUnderTest = systemUnderTest;
        _remoteServers = remoteServers;
        _clientApps = clientApps;
        _issuers = issuers;
        _virtualHosts = virtualHosts;
        _scrubber = scrubber;
        _log = log;
    }

    public Permutation Permutation { get; }

    /// <summary>The one clock shared by every host in this environment (D15).</summary>
    public TestClock Clock { get; }

    /// <summary>The system under test's nodes. Only for routing in regression tests (D12, D36).</summary>
    public IReadOnlyList<IdentityServerNode> Nodes => _systemUnderTest.Nodes;

    /// <summary>The system under test's load balancer. Only for routing in regression tests (D12, D36).</summary>
    public TestLoadBalancer LoadBalancer => _systemUnderTest.LoadBalancer;

    /// <summary>
    /// Creates a new user agent with its own cookie jar, reaching every virtual host in this environment. The
    /// environment disposes it.
    /// </summary>
    public TestBrowser CreateBrowser()
    {
        var browser = new TestBrowser(_virtualHosts.CreateHandler("browser", handler =>
        {
            handler.UseCookies = false; // The browser's CookieJarHandler keeps the cookies, on the test clock.
            handler.AllowAutoRedirect = false;
        }), Clock, _scrubber);
        _browsers.Add(browser);
        return browser;
    }

    /// <summary>
    /// Creates a plain <see cref="HttpClient" /> that reaches every virtual host in this environment, for calling the
    /// token and introspection endpoints directly, the way a client app or an API would, instead of through the
    /// <see cref="TestBrowser" /> (design doc section 09). Requests go through the load balancer to the issuer's URL,
    /// use the harness's no-DNS and trusted-TLS plumbing, and are logged and numbered like every other harness request
    /// (see <see cref="VirtualHostRegistry" />). The environment disposes it.
    /// </summary>
    public HttpClient CreateBackChannelClient(string name = "back channel")
    {
        var client = new HttpClient(_virtualHosts.CreateHandler(name));
        _backChannelClients.Add(client);
        return client;
    }

    /// <summary>
    /// Seeds data for every issuer, one after another, before any scenario runs (D35), so a leak between spaces has
    /// data to leak.
    /// </summary>
    public async Task SetupData(Func<IIssuer, TResources, Task> setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        foreach (var (issuer, resources) in _issuers)
        {
            using var step = _log.Measure($"SetupData for {issuer.Name}");
            await setup(issuer, resources);
        }
    }

    /// <summary>
    /// Runs the scenario for each issuer in turn and compares every result with the test's single snapshot (D28),
    /// whatever the permutation. The snapshot holds the browser's journal of every request the scenario made, and what
    /// the scenario returned. Results are made readable first, and scrubbed for the issuer the scenario ran
    /// against (section 10), so every issuer must produce exactly the same snapshot.
    /// </summary>
    public async Task VerifyScenario<TResult>(
        Func<IIssuer, TResources, Task<TResult>> scenario,
        [CallerFilePath] string sourceFile = "")
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var cancellationToken = TestContext.Current.CancellationToken;

        foreach (var (issuer, resources) in _issuers)
        {
            using var run = _log.Measure($"VerifyScenario for {issuer.Name}");
            _scrubber.BeginRun();
            LogPlaceholders(issuer.Name);
            foreach (var browser in _browsers)
            {
                browser.ClearJournal();
            }
            TResult result;
            using (_log.Measure("scenario"))
            {
                result = await scenario(issuer, resources);
            }

            var snapshot = SnapshotWithJournals(await ScenarioResultFormatter.FormatAsync(result, _scrubber, Clock, cancellationToken));
            (result as IDisposable)?.Dispose();

            var settings = new VerifySettings();
            settings.IgnoreParametersForVerified(); // One .verified file for every permutation (D28).
            settings.DisableRequireUniquePrefix(); // Every issuer compares against that same file.

            // Values are scrubbed by registration, not by pattern (section 10). Verify's own GUID and date scrubbing
            // would run first and hide which registered value (a user, a client) appeared where.
            settings.DontScrubGuids();
            settings.DontScrubDateTimes();
            var currentIssuer = issuer.Name;
            settings.AddScrubber(builder => _scrubber.Scrub(builder, currentIssuer));

            using (_log.Measure("Verify"))
            {
                await Verifier.Verify(snapshot, settings, sourceFile);
            }
        }
    }

    // What every placeholder in this run's snapshot stands for, so the scrubbed values can be checked by eye.
    private void LogPlaceholders(string issuer)
    {
        _log.Write($"placeholders for {issuer}'s run:");
        foreach (var (placeholder, values) in _scrubber.DescribeAll(issuer))
        {
            _log.Write($"  {placeholder} = {string.Join(", ", values)}");
        }
    }

    // The browsers' journals show every hop, including automatic redirects; the result shows where the scenario ended (D39).
    private object? SnapshotWithJournals(object? result)
    {
        var journals = _browsers.Where(browser => browser.Journal.Count > 0).ToList();
        return journals.Count switch
        {
            0 => result,
            1 => new { Steps = journals[0].Journal, Result = result },
            _ => new { Browsers = journals.Select(browser => browser.Journal).ToList(), Result = result },
        };
    }

    internal static async Task<TestEnvironment<TResources>> StartAsync(
        Permutation permutation,
        Func<TestEnvironmentBuilder, PerIssuer<TResources>> setup,
        Ct cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permutation);
        ArgumentNullException.ThrowIfNull(setup);
        if (!permutation.IsValid)
        {
            throw new ArgumentException($"Permutation {permutation} is not valid (design doc D32).", nameof(permutation));
        }

        if (permutation.NotImplementedReason is { } reason)
        {
            throw new NotSupportedException($"{reason}. Permutation: {permutation}.");
        }

        var log = new HarnessLog();
        var clock = new TestClock();
        var virtualHosts = new VirtualHostRegistry(log);
        var scrubber = new SnapshotScrubber();

        var spacesOn = permutation.Spaces == SpacesMode.On;
        var systemUnderTest = new IdentityServerTopology(
            TestEnvironment.SystemUnderTestName,
            TestEnvironment.SystemUnderTestName,
            owner: null,
            isSystemUnderTest: true,
            permutation.Store,
            permutation.Caching,
            permutation.Users,
            spacesOn,
            scrubber,
            clock);

        // Without Spaces there is one issuer: the system under test itself. With Spaces there is one issuer per space
        // (section 09): one matched by host name, one by path.
        List<Issuer> issuers = spacesOn
            ?
            [
                new Issuer("space-1", systemUnderTest, scrubber, new Uri($"https://{Space1Host}"), new SpaceMatchPattern { Origin = $"https://{Space1Host}" }),
                new Issuer("space-2", systemUnderTest, scrubber, new Uri("https://sut/t/space-2"), new SpaceMatchPattern { Path = "/space-2" }),
            ]
            : [new Issuer(TestEnvironment.SystemUnderTestName, systemUnderTest, scrubber)];
        foreach (var issuer in issuers)
        {
            scrubber.Register(issuer.Url.GetLeftPart(UriPartial.Path).TrimEnd('/'), TestEnvironment.SystemUnderTestName, owner: issuer.Name);
        }

        var builder = new TestEnvironmentBuilder(systemUnderTest, issuers, scrubber, clock);
        PerIssuer<TResources> perIssuer;
        using (log.Measure("setup lambda"))
        {
            perIssuer = setup(builder);
        }

        if (perIssuer is null)
        {
            throw new InvalidOperationException("The setup lambda must return the result of env.SetupPerIssuer(...).");
        }

        var environment = new TestEnvironment<TResources>(
            permutation, clock, systemUnderTest, builder.RemoteServers, builder.ClientApps, perIssuer.Issuers, virtualHosts, scrubber, log);
        try
        {
            using var start = log.Measure("environment start");
            foreach (var remote in builder.RemoteServers)
            {
                await remote.StartAsync(clock, virtualHosts, scrubber, log, cancellationToken);
            }

            await systemUnderTest.StartAsync(clock, virtualHosts, scrubber, log, cancellationToken);
            if (spacesOn)
            {
                virtualHosts.Register(Space1Host, systemUnderTest.LoadBalancer.Port, $"IdentityServer {TestEnvironment.SystemUnderTestName}");
                using var spacesStep = log.Measure("create spaces");
                await CreateSpacesAsync(systemUnderTest, issuers, cancellationToken);
            }

            foreach (var app in builder.ClientApps)
            {
                using var appStep = log.Measure($"client app {app.Name}");
                await app.StartAsync(clock, virtualHosts, log, cancellationToken);
            }
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }

        return environment;
    }

    private static async Task CreateSpacesAsync(
        IdentityServerTopology systemUnderTest,
        IEnumerable<Issuer> issuers,
        Ct cancellationToken)
    {
        await using var scope = systemUnderTest.Nodes[0].Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<ISpaceAdmin>();
        var spaces = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var storage = (DuendeStorageStoreProvider)systemUnderTest.StoreProvider;
        foreach (var issuer in issuers)
        {
            var result = await admin.CreateAsync(new CreateSpaceConfiguration { Name = issuer.Name, MatchPatterns = [issuer.SpaceMatch!] }, cancellationToken);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Spaces rejected space {issuer.Name}: {string.Join("; ", result.Errors?.Select(error => error.ToString()) ?? [])}");
            }

            issuer.SpaceId = result.Id;
            using (spaces.SetSpace(result.Id!))
            {
                await storage.InitializeSpaceAsync(cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        using var step = _log.Measure("environment dispose");
        foreach (var browser in _browsers)
        {
            browser.Dispose();
        }

        foreach (var client in _backChannelClients)
        {
            client.Dispose();
        }

        foreach (var app in _clientApps)
        {
            await app.DisposeAsync();
        }

        await _systemUnderTest.DisposeAsync();
        foreach (var remote in _remoteServers)
        {
            await remote.DisposeAsync();
        }
    }
}
