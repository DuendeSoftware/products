// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace Duende.IdentityServer.EntityFramework.Interfaces;

/// <summary>
/// Abstraction for the configuration context.
/// </summary>
/// <seealso cref="System.IDisposable" />
public interface IConfigurationDbContext : IDisposable
{
    /// <summary>
    /// Gets or sets the clients.
    /// </summary>
    /// <value>
    /// The clients.
    /// </value>
    DbSet<Client> Clients { get; set; }

    /// <summary>
    /// Gets or sets the clients' CORS origins.
    /// </summary>
    /// <value>
    /// The clients CORS origins.
    /// </value>
    DbSet<ClientCorsOrigin> ClientCorsOrigins { get; set; }

    /// <summary>
    /// Gets or sets the identity resources.
    /// </summary>
    /// <value>
    /// The identity resources.
    /// </value>
    DbSet<IdentityResource> IdentityResources { get; set; }

    /// <summary>
    /// Gets or sets the API resources.
    /// </summary>
    /// <value>
    /// The API resources.
    /// </value>
    DbSet<ApiResource> ApiResources { get; set; }

    /// <summary>
    /// Gets or sets the scopes.
    /// </summary>
    /// <value>
    /// The identity resources.
    /// </value>
    DbSet<ApiScope> ApiScopes { get; set; }

    /// <summary>
    /// Gets or sets the identity providers.
    /// </summary>
    /// <value>
    /// The identity providers.
    /// </value>
    DbSet<IdentityProvider> IdentityProviders { get; set; }

    /// <summary>
    /// Gets or sets the SAML service providers.
    /// </summary>
    /// <value>
    /// The SAML service providers.
    /// </value>
    DbSet<SamlServiceProvider> SamlServiceProviders { get; set; }

    /// <summary>
    /// Saves the changes.
    /// </summary>
    /// <returns></returns>
    Task<int> SaveChangesAsync(Ct ct);

    // this is here only because of this: https://github.com/DuendeSoftware/IdentityServer/issues/472
    // and because Microsoft implements the old API explicitly: https://github.com/dotnet/aspnetcore/blob/v6.0.0-rc.2.21480.10/src/Identity/ApiAuthorization.IdentityServer/src/Data/ApiAuthorizationDbContext.cs

    /// <summary>
    /// Saves the changes.
    /// </summary>
    /// <returns></returns>
    Task<int> SaveChangesAsync() => SaveChangesAsync(Ct.None);
}
