// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// The default data tests add explicitly with <c>AddDefault…</c> methods (design doc D22). Every property returns a
/// new instance, so a test can adjust what it gets without affecting other tests or other nodes.
/// </summary>
public static class TestDefaults
{
    /// <summary>The scopes a default client asks for: only default identity resources and API scopes.</summary>
    public static IReadOnlyList<string> ClientScopes =>
        [IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile, ApiScopes.Api1.Name];

    public static class IdentityResources
    {
        public static IdentityResource OpenId => new Duende.IdentityServer.Models.IdentityResources.OpenId();

        public static IdentityResource Profile => new Duende.IdentityServer.Models.IdentityResources.Profile();

        public static IdentityResource Email => new Duende.IdentityServer.Models.IdentityResources.Email();

        public static IReadOnlyList<IdentityResource> All => [OpenId, Profile, Email];
    }

    public static class ApiScopes
    {
        public static ApiScope Api1 => new("api1", "API 1");

        public static IReadOnlyList<ApiScope> All => [Api1];
    }
}

/// <summary>Creates test clients scoped to one issuer (design doc section 08, D14).</summary>
public interface ITestClients
{
    /// <summary>
    /// A complete authorization code client for <paramref name="clientApp" />: its client ID, secret, redirect and
    /// post-logout redirect URIs come from the app, it requires PKCE, and it may ask for <see cref="TestDefaults.ClientScopes" />.
    /// </summary>
    Client CodeFlow(IWebApp clientApp);
}

internal sealed class TestClients : ITestClients
{
    public Client CodeFlow(IWebApp clientApp)
    {
        ArgumentNullException.ThrowIfNull(clientApp);
        return new Client
        {
            ClientId = clientApp.ClientId,
            ClientName = clientApp.Name,
            ClientSecrets = { TestSecrets.Create(clientApp.ClientSecret) },
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RedirectUris = { clientApp.RedirectUri.ToString() },
            PostLogoutRedirectUris = { clientApp.PostLogoutRedirectUri.ToString() },
            AllowedScopes = [.. TestDefaults.ClientScopes],
        };
    }
}

/// <summary>
/// <c>With…</c> methods that adjust a client a factory already made complete (design doc D14): a test adds only what
/// the scenario needs, for example <see cref="WithRefreshTokens" /> for offline_access, or
/// <see cref="WithReferenceTokens" /> so its access tokens are opaque reference tokens instead of JWTs.
/// </summary>
public static class ClientBuilderExtensions
{
    /// <summary>Allows offline access and asks for it by default, so a code flow also returns a refresh token.</summary>
    public static Client WithRefreshTokens(this Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.AllowOfflineAccess = true;
        client.AllowedScopes.Add(IdentityServerConstants.StandardScopes.OfflineAccess);
        return client;
    }

    /// <summary>Issues reference (opaque) access tokens instead of JWTs, so an API must introspect them to use them.</summary>
    public static Client WithReferenceTokens(this Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.AccessTokenType = AccessTokenType.Reference;
        return client;
    }

    /// <summary>
    /// Allows <paramref name="clientApp" />'s <see cref="IWebApp.ManualCallbackUrl" /> as a redirect URI, for a scenario
    /// that redeems its own authorization code there (<see cref="TestBrowser.AuthorizeAsync" />) instead of through the
    /// app's OpenID Connect handler.
    /// </summary>
    public static Client WithManualCallback(this Client client, IWebApp clientApp)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(clientApp);
        client.RedirectUris.Add(clientApp.ManualCallbackUrl.ToString());
        return client;
    }
}

/// <summary>
/// Client secrets created by the harness. IdentityServer's models hold a secret's hash, but Duende.Storage's admin API
/// takes the plaintext and hashes it itself, so the harness remembers the plaintext of every secret it creates.
/// </summary>
internal static class TestSecrets
{
    private static readonly ConditionalWeakTable<Secret, string> Plaintexts = new();

    public static Secret Create(string plaintext)
    {
        var secret = new Secret(plaintext.Sha256());
        Plaintexts.AddOrUpdate(secret, plaintext);
        return secret;
    }

    public static string? PlaintextOf(Secret secret) => Plaintexts.TryGetValue(secret, out var plaintext) ? plaintext : null;
}

/// <summary>
/// The <see cref="ITestStorage" /> of an issuer or a remote server. It writes through the topology's store provider
/// (in-memory, Entity Framework or Duende.Storage, from the permutation) and keeps users as IdentityServer test users on
/// every node (D20).
/// </summary>
/// <param name="name">The issuer or server the storage belongs to; names its client at remote IdPs.</param>
/// <param name="owner">The issuer whose values these are, for scrubbing.</param>
/// <param name="url">The issuer's public URL.</param>
/// <param name="server">The server whose stores are written.</param>
/// <param name="scrubber">Where generated client IDs and secrets are registered.</param>
internal sealed class TestStorage(string name, string? owner, Uri url, IdentityServerTopology server, SnapshotScrubber scrubber) : ITestStorage
{
    private static Ct Ct => TestContext.Current.CancellationToken;

    private IStoreProvider Stores => server.StoreProvider;

    /// <summary>The space this storage writes into, or <c>null</c> without Spaces.</summary>
    public SpaceId? SpaceId { get; set; }

    public async Task AddDefaultsAsync()
    {
        await AddDefaultIdentityResourcesAsync();
        await AddDefaultApiScopesAsync();
    }

    public async Task AddDefaultIdentityResourcesAsync()
    {
        foreach (var resource in TestDefaults.IdentityResources.All)
        {
            await InSpaceAsync(stores => stores.AddIdentityResourceAsync(resource, Ct));
        }
    }

    public async Task AddDefaultApiScopesAsync()
    {
        foreach (var scope in TestDefaults.ApiScopes.All)
        {
            await InSpaceAsync(stores => stores.AddApiScopeAsync(scope, Ct));
        }
    }

    public Task AddClientAsync(Client client) => InSpaceAsync(stores => stores.AddClientAsync(client, Ct));

    public Task UpdateClientAsync(Client client) => InSpaceAsync(stores => stores.UpdateClientAsync(client, Ct));

    public Task RemoveClientAsync(string clientId) => InSpaceAsync(stores => stores.RemoveClientAsync(clientId, Ct));

    public Task AddApiScopeAsync(ApiScope scope) => InSpaceAsync(stores => stores.AddApiScopeAsync(scope, Ct));

    public Task AddApiResourceAsync(ApiResource resource) => InSpaceAsync(stores => stores.AddApiResourceAsync(resource, Ct));

    public Task AddIdentityResourceAsync(IdentityResource resource) => InSpaceAsync(stores => stores.AddIdentityResourceAsync(resource, Ct));

    public Task AddUserAsync(TestUser user)
    {
        if (server.UsersMode == UserStore.TestUsers)
        {
            server.Users.AddUser(user, SpaceId);
            return Task.CompletedTask;
        }

        // D20: with User Management on, users go through it only, in the same space (if any) the test asked for.
        // Writes through the store provider itself (D3), the same as every other write here, instead of a started
        // node's services directly, so this also works before any node has started.
        var storage = (DuendeStorageStoreProvider)Stores;
        return storage.AddUserManagementUserAsync(user, SpaceId, Ct);
    }

    public async Task AddRemoteIdpAsync(IIdentityServer idp, Protocol protocol)
    {
        ArgumentNullException.ThrowIfNull(idp);
        if (protocol != Protocol.Oidc)
        {
            throw new NotSupportedException($"Federating over {protocol} is not implemented in the outside-in harness yet.");
        }

        var upstream = (IdentityServerTopology)idp;
        var scheme = upstream.Name;
        var clientId = $"{name}-{Guid.NewGuid():N}";
        var clientSecret = Guid.NewGuid().ToString("N");
        scrubber.Register(clientId, $"client:{name}", owner);
        scrubber.Register(clientSecret, $"secret:{name}", owner);

        // The remote IdP is our own test host (D15), so the harness also adds what it needs to serve this issuer.
        foreach (var resource in new[] { TestDefaults.IdentityResources.OpenId, TestDefaults.IdentityResources.Profile })
        {
            if (!await upstream.StoreProvider.HasIdentityResourceAsync(resource.Name, Ct))
            {
                await upstream.StoreProvider.AddIdentityResourceAsync(resource, Ct);
            }
        }

        var callbackBase = new Uri($"{url.GetLeftPart(UriPartial.Path).TrimEnd('/')}/federation/{scheme}/");
        await upstream.StoreProvider.AddClientAsync(
            new Client
            {
                ClientId = clientId,
                ClientName = name,
                ClientSecrets = { TestSecrets.Create(clientSecret) },
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { new Uri(callbackBase, "signin").ToString() },
                PostLogoutRedirectUris = { new Uri(callbackBase, "signout-callback").ToString() },
                AllowedScopes = { IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile },
            },
            Ct);

        // A dynamic provider (design doc section 05, ProtocolModules), so it can be added after the servers start.
        await InSpaceAsync(stores => stores.AddIdentityProviderAsync(
            new OidcProvider
            {
                Scheme = scheme,
                DisplayName = upstream.Name,
                Authority = upstream.BaseUrl.GetLeftPart(UriPartial.Authority),
                ClientId = clientId,
                ClientSecret = clientSecret,
                ResponseType = "code",
                Scope = $"{IdentityServerConstants.StandardScopes.OpenId} {IdentityServerConstants.StandardScopes.Profile}",
                UsePkce = true,
                GetClaimsFromUserInfoEndpoint = true,
            },
            Ct));
    }

    private async Task InSpaceAsync(Func<IStoreProvider, Task> write)
    {
        if (SpaceId is null)
        {
            await write(Stores);
            return;
        }

        var accessor = server.Nodes[0].Services.GetRequiredService<ISpaceContextAccessor>();
        using (accessor.SetSpace(SpaceId))
        {
            await write(Stores);
        }
    }
}
