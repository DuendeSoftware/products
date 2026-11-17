// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// A load-balanced IdentityServer: the system under test, or a remote server. Exists from the moment it's declared in
/// the setup lambda, so handles can reference each other before anything runs.
/// </summary>
internal sealed class IdentityServerTopology : IIdentityServer, IAsyncDisposable
{
    /// <summary>Every topology runs 2 nodes (design doc D36). Tests don't see or set this.</summary>
    internal const int NodeCount = 2;

    private readonly string? _owner;
    private readonly bool _isSystemUnderTest;
    private readonly UserStore _users;
    private readonly bool _spacesOn;
    private readonly RSA _signingRsa = RSA.Create(2048);
    private IReadOnlyList<IdentityServerNode>? _nodes;
    private TestLoadBalancer? _loadBalancer;

    /// <param name="name">The placeholder name, for example <c>sut</c> or <c>upstream</c>.</param>
    /// <param name="hostName">The virtual host name clients connect to. Unique within the environment.</param>
    /// <param name="owner">The issuer this server belongs to, if it was added for one issuer in <c>SetupPerIssuer</c>.</param>
    /// <param name="isSystemUnderTest">The system under test's URL is registered for scrubbing by its issuers, not here.</param>
    /// <param name="store">The store type the server runs on. Remote servers always use the in-memory store.</param>
    /// <param name="caching">
    /// The caching mode the server's nodes run (design doc D34). <see cref="CachingMode.InMemory" /> gives each node its
    /// own cache; <see cref="CachingMode.Distributed" /> shares one <see cref="IDistributedCache" /> instance between
    /// both nodes. Remote servers always run with no caching: they are not the system under test.
    /// </param>
    /// <param name="users">
    /// Where users live (D20). <see cref="UserStore.UserManagement" /> is only valid on Duende.Storage (D32); with it,
    /// users and sign-in go through User Management only, never the test users.
    /// </param>
    /// <param name="spacesOn">Whether the server runs Spaces. Only valid on Duende.Storage.</param>
    /// <param name="scrubber">Where the server's own storage registers generated values.</param>
    /// <param name="clock">The environment's clock, which the shared distributed cache expires entries by (D15).</param>
    public IdentityServerTopology(string name, string hostName, string? owner, bool isSystemUnderTest, StoreType store, CachingMode caching, UserStore users, bool spacesOn, SnapshotScrubber scrubber, TestClock clock)
    {
        Name = name;
        HostName = hostName;
        _owner = owner;
        _isSystemUnderTest = isSystemUnderTest;
        _users = users;
        _spacesOn = spacesOn;

        // One distributed cache shared by both nodes in the topology (D34): what one node caches, the other reads
        // from the same backing store, instead of each node holding its own copy. Wrapped in SharedDistributedCache
        // rather than exposed as a plain MemoryDistributedCache, because HybridCache 10.0 drops its L2 tier when the
        // backend's concrete type is MemoryDistributedCache and L1 is a plain MemoryCache (see that type's doc comment).
        IDistributedCache? distributedCache = caching == CachingMode.Distributed
            ? new SharedDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions { Clock = new TestSystemClock(clock) })))
            : null;

        StoreProvider = store switch
        {
            StoreType.InMemory => new InMemoryStoreProvider(Users, caching, distributedCache),
            StoreType.EntityFramework => new EntityFrameworkStoreProvider(caching, distributedCache),
            StoreType.DuendeStorageSchemasInMemory => new DuendeStorageStoreProvider(schemasInDatabase: false, spacesOn, users == UserStore.UserManagement, caching, distributedCache),
            StoreType.DuendeStorageSchemasInDatabase => new DuendeStorageStoreProvider(schemasInDatabase: true, spacesOn, users == UserStore.UserManagement, caching, distributedCache),
            _ => throw new ArgumentOutOfRangeException(nameof(store), store, "Unknown store type."),
        };
        Storage = new TestStorage(name, owner, BaseUrl, this, scrubber);
        Builder = new IdentityServerTopologyBuilder(users);
    }

    public string Name { get; }

    public string HostName { get; }

    public Uri BaseUrl => new($"https://{HostName}");

    public IdentityServerTopologyBuilder Builder { get; }

    /// <summary>Where this topology's users live (D20): test users, or User Management on Duende.Storage.</summary>
    public UserStore UsersMode => _users;

    /// <summary>
    /// Every node's test users (D20), and with the in-memory store type also its configuration and operational stores.
    /// Unused when <see cref="UsersMode" /> is <see cref="UserStore.UserManagement" />.
    /// </summary>
    public InMemoryTopologyStores Users { get; } = new(NodeCount);

    /// <summary>The store type the topology runs on (design doc section 05, <c>IStoreProvider</c>).</summary>
    public IStoreProvider StoreProvider { get; }

    public ITestStorage Storage { get; }

    public IReadOnlyList<IdentityServerNode> Nodes => _nodes ?? throw NotStarted();

    public TestLoadBalancer LoadBalancer => _loadBalancer ?? throw NotStarted();

    public async Task StartAsync(TestClock clock, VirtualHostRegistry virtualHosts, SnapshotScrubber scrubber, HarnessLog log, Ct cancellationToken)
    {
        using var step = log.Measure($"server {Name} start");
        foreach (var user in Builder.InitialUsers)
        {
            Users.AddUser(user);
        }

        // Signing and data protection keys are shared per topology, so what one node issues the other accepts (section 03).
        RsaSecurityKey signingKey;
        using (log.Measure("signing key generation"))
        {
            _ = _signingRsa.ExportParameters(includePrivateParameters: false); // RSA generates its key lazily, on first use.
            signingKey = new RsaSecurityKey(_signingRsa) { KeyId = $"{Name}-signing-key" };
        }

        SharedDataProtection dataProtection;
        using (log.Measure("data protection key ring"))
        {
            dataProtection = new SharedDataProtection($"identity-server:{HostName}", clock);
        }

        var nodes = new List<IdentityServerNode>();
        _nodes = nodes;
        for (var i = 0; i < NodeCount; i++)
        {
            var context = new NodeContext(clock, signingKey, dataProtection, Users, Users.Nodes[i], StoreProvider, i, virtualHosts, Builder.LoginPage, _users, _spacesOn, log);
            nodes.Add(await IdentityServerNode.StartAsync($"{Name}-node-{i + 1}", context, cancellationToken));
        }

        using (log.Measure("load balancer"))
        {
            _loadBalancer = await TestLoadBalancer.StartAsync(Name, nodes, clock, log, cancellationToken);
        }

        virtualHosts.Register(HostName, _loadBalancer.Port, $"IdentityServer {Name}");

        if (!_isSystemUnderTest)
        {
            scrubber.Register(BaseUrl.GetLeftPart(UriPartial.Authority), Name, _owner);
        }

        scrubber.Register($"https://127.0.0.1:{_loadBalancer.Port}", Name, _owner);
        scrubber.Register($"127.0.0.1:{_loadBalancer.Port}", Name, _owner);
        foreach (var node in nodes)
        {
            scrubber.Register(node.DirectUrl.GetLeftPart(UriPartial.Authority), node.Id, _owner);
            scrubber.Register($"127.0.0.1:{node.Port}", node.Id, _owner);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_loadBalancer is not null)
        {
            await _loadBalancer.DisposeAsync();
        }

        foreach (var node in _nodes ?? [])
        {
            await node.DisposeAsync();
        }

        await StoreProvider.DisposeAsync();
        _signingRsa.Dispose();
    }

    private InvalidOperationException NotStarted() =>
        new($"IdentityServer '{Name}' has not started yet. Use this after TestEnvironment.StartAsync completes.");
}

/// <summary>Everything a node needs from its topology and environment.</summary>
internal sealed record NodeContext(
    TestClock Clock,
    SecurityKey SigningKey,
    SharedDataProtection DataProtection,
    InMemoryTopologyStores TopologyStores,
    NodeStores NodeStores,
    IStoreProvider StoreProvider,
    int NodeIndex,
    VirtualHostRegistry VirtualHosts,
    Action<Microsoft.AspNetCore.Builder.WebApplication>? LoginPage,
    UserStore Users,
    bool SpacesOn,
    HarnessLog Log);
