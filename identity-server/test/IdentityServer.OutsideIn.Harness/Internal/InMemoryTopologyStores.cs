// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using DuendeTestUser = Duende.IdentityServer.Test.TestUser;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

// STAND-IN FOR DESIGN DECISION D17. D17 says IdentityServer's shipped in-memory stores become writable through a
// public API change. Until that change exists, the harness uses these writable stores of its own. They are registered
// in place of AddInMemoryClients/AddInMemoryIdentityResources/AddInMemoryApiScopes/AddInMemoryIdentityProviders, and
// should be deleted once the product stores accept writes after start.

/// <summary>
/// The in-memory store state of one topology (design doc sections 03 and 04). Configuration data (clients, resources,
/// identity providers, users) is per node, and every write fans out to all nodes (D4). Operational data (grants,
/// device codes, pushed authorization requests) is one instance shared by every node, like a database would be.
/// </summary>
internal sealed class InMemoryTopologyStores
{
    private readonly Lock _usersLock = new();

    public InMemoryTopologyStores(int nodeCount) =>
        Nodes = Enumerable.Range(0, nodeCount).Select(_ => new NodeStores()).ToArray();

    public IReadOnlyList<NodeStores> Nodes { get; }

    public InMemoryPersistedGrantStore PersistedGrants { get; } = new();

    public InMemoryDeviceFlowStore DeviceFlow { get; } = new();

    public InMemoryPushedAuthorizationRequestStore PushedAuthorizationRequests { get; } = new();

    public void AddClient(Client client) => ForEachNode(node => node.Clients.Add(client));

    public void RemoveClient(string clientId) => ForEachNode(node => node.Clients.Remove(clientId));

    public void AddIdentityResource(IdentityResource resource) => ForEachNode(node => node.Resources.Add(resource));

    public void AddApiScope(ApiScope scope) => ForEachNode(node => node.Resources.Add(scope));

    public void AddApiResource(ApiResource resource) => ForEachNode(node => node.Resources.Add(resource));

    public void AddIdentityProvider(IdentityProvider provider) => ForEachNode(node => node.IdentityProviders.Add(provider));

    public bool HasIdentityResource(string name) => Nodes[0].Resources.HasIdentityResource(name);

    public void AddUser(TestUser user, SpaceId? spaceId = null) =>
        AddUser(
            new DuendeTestUser
            {
                SubjectId = user.SubjectId,
                Username = user.Username,
                Password = user.Password,
                Claims = { new Claim(JwtClaimTypes.Name, user.Username) },
            },
            spaceId);

    /// <summary>
    /// Creates a local user for someone who signed in at an external provider (the external login page calls this).
    /// The local subject ID is the external one, so the same person is recognizable across servers in snapshots. The
    /// user belongs to the space the sign-in happened in, or to no space without Spaces.
    /// </summary>
    public DuendeTestUser ProvisionExternalUser(string provider, string externalSubjectId, string name, SpaceId? spaceId)
    {
        var user = new DuendeTestUser
        {
            SubjectId = externalSubjectId,
            Username = name,
            ProviderName = provider,
            ProviderSubjectId = externalSubjectId,
            Claims = { new Claim(JwtClaimTypes.Name, name) },
        };
        AddUser(user, spaceId);
        return user;
    }

    private void AddUser(DuendeTestUser user, SpaceId? spaceId)
    {
        // TestUserStore reads its list without locking. Users are added rarely, and never while a node reads the
        // same list in this slice's scenarios.
        lock (_usersLock)
        {
            ForEachNode(node => node.UsersOf(spaceId).Add(user));
        }
    }

    private void ForEachNode(Action<NodeStores> write)
    {
        foreach (var node in Nodes)
        {
            write(node);
        }
    }
}

/// <summary>One node's own copy of the configuration stores.</summary>
internal sealed class NodeStores
{
    public WritableClientStore Clients { get; } = new();

    public WritableResourceStore Resources { get; } = new();

    public WritableIdentityProviderStore IdentityProviders { get; } = new();

    private readonly ConcurrentDictionary<SpaceId, List<DuendeTestUser>> _spaceUsers = new();

    /// <summary>Handed to IdentityServer's <c>AddTestUsers</c>, which keeps a reference to this list. Without Spaces, these are all the users.</summary>
    public List<DuendeTestUser> Users { get; } = [];

    /// <summary>The users of one space, or all users when <paramref name="spaceId" /> is <c>null</c> (Spaces off).</summary>
    public List<DuendeTestUser> UsersOf(SpaceId? spaceId) => spaceId is null ? Users : _spaceUsers.GetOrAdd(spaceId, _ => []);
}

internal sealed class WritableClientStore : IClientStore
{
    private readonly ConcurrentDictionary<string, Client> _clients = new(StringComparer.Ordinal);

    public void Add(Client client) => _clients[client.ClientId] = client;

    public void Remove(string clientId) => _clients.TryRemove(clientId, out _);

    public Task<Client?> FindClientByIdAsync(string clientId, Ct ct) => Task.FromResult(_clients.GetValueOrDefault(clientId));

    public IAsyncEnumerable<Client> GetAllClientsAsync(Ct ct) => _clients.Values.ToAsyncEnumerable();
}

/// <summary>Holds the resources and answers queries through IdentityServer's own <see cref="InMemoryResourcesStore" />.</summary>
internal sealed class WritableResourceStore : IResourceStore
{
    private readonly Lock _lock = new();
    private readonly List<IdentityResource> _identityResources = [];
    private readonly List<ApiResource> _apiResources = [];
    private readonly List<ApiScope> _apiScopes = [];

    public void Add(IdentityResource resource) => Write(() => Replace(_identityResources, resource, r => r.Name == resource.Name));

    public void Add(ApiResource resource) => Write(() => Replace(_apiResources, resource, r => r.Name == resource.Name));

    public void Add(ApiScope scope) => Write(() => Replace(_apiScopes, scope, s => s.Name == scope.Name));

    public bool HasIdentityResource(string name)
    {
        lock (_lock)
        {
            return _identityResources.Any(resource => resource.Name == name);
        }
    }

    public Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct) =>
        Snapshot().FindIdentityResourcesByScopeNameAsync(scopeNames, ct);

    public Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct) =>
        Snapshot().FindApiScopesByNameAsync(scopeNames, ct);

    public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct) =>
        Snapshot().FindApiResourcesByScopeNameAsync(scopeNames, ct);

    public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct ct) =>
        Snapshot().FindApiResourcesByNameAsync(apiResourceNames, ct);

    public Task<Resources> GetAllResourcesAsync(Ct ct) => Snapshot().GetAllResourcesAsync(ct);

    private static void Replace<T>(List<T> items, T item, Predicate<T> sameName)
    {
        items.RemoveAll(sameName);
        items.Add(item);
    }

    private void Write(Action write)
    {
        lock (_lock)
        {
            write();
        }
    }

    private InMemoryResourcesStore Snapshot()
    {
        lock (_lock)
        {
            return new InMemoryResourcesStore([.. _identityResources], [.. _apiResources], [.. _apiScopes]);
        }
    }
}

internal sealed class WritableIdentityProviderStore : IIdentityProviderStore
{
    private readonly ConcurrentDictionary<string, IdentityProvider> _providers = new(StringComparer.Ordinal);

    public void Add(IdentityProvider provider) => _providers[provider.Scheme] = provider;

    public Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct) =>
        Task.FromResult<IReadOnlyCollection<IdentityProviderName>>(_providers.Values
            .Select(provider => new IdentityProviderName { Scheme = provider.Scheme, DisplayName = provider.DisplayName, Enabled = provider.Enabled })
            .ToList());

    public Task<IdentityProvider?> GetBySchemeAsync(string scheme, Ct ct) => Task.FromResult(_providers.GetValueOrDefault(scheme));
}
