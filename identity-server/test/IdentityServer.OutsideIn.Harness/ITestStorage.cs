// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// What a test calls, through <see cref="IIssuer.Storage" />, to seed and change one issuer's data (design doc
/// sections 04, 05 and 08). Writes go to the store type of the permutation: to every node for the in-memory store (D4),
/// or to the topology's shared databases for Entity Framework and Duende.Storage. Users go to IdentityServer's test
/// users, or to User Management when it's on (D20). The design has every call work before and after the servers start
/// (D3); in this slice, database-backed writes need the first node to have initialized the store. Verification reads
/// (D7) are not part of this slice.
/// </summary>
public interface ITestStorage
{
    /// <summary>Adds every default set: <see cref="AddDefaultIdentityResourcesAsync" /> and <see cref="AddDefaultApiScopesAsync" /> (D22).</summary>
    Task AddDefaultsAsync();

    /// <summary>Adds <see cref="TestDefaults.IdentityResources.All" />.</summary>
    Task AddDefaultIdentityResourcesAsync();

    /// <summary>Adds <see cref="TestDefaults.ApiScopes.All" />.</summary>
    Task AddDefaultApiScopesAsync();

    Task AddClientAsync(Client client);

    Task UpdateClientAsync(Client client);

    Task RemoveClientAsync(string clientId);

    Task AddApiScopeAsync(ApiScope scope);

    Task AddApiResourceAsync(ApiResource resource);

    Task AddIdentityResourceAsync(IdentityResource resource);

    /// <summary>
    /// Adds a user. Without User Management, users are IdentityServer test users (D20); with it, users go through
    /// User Management only, in the same space (if any) this storage writes to.
    /// </summary>
    Task AddUserAsync(TestUser user);

    /// <summary>
    /// Lets this issuer's users sign in at <paramref name="idp" />: registers it as an external provider here, and this
    /// issuer as a client there.
    /// </summary>
    Task AddRemoteIdpAsync(IIdentityServer idp, Protocol protocol);
}

/// <summary>The protocol an issuer uses to federate with a remote IdP.</summary>
public enum Protocol
{
    Oidc,

    /// <summary>Not implemented in this slice of the harness.</summary>
    Saml2,
}
