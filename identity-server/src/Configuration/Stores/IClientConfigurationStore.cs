// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Defines the contract for a service that persists client configurations to the underlying
/// data store. This store is used by the dynamic client registration pipeline to save newly
/// registered clients so that they are available for subsequent authorization requests.
/// </summary>
/// <remarks>
/// The Entity Framework-based implementation persists clients to the IdentityServer
/// configuration database and is registered automatically when using the Entity Framework
/// configuration stores.
/// <para>
/// To use a different persistence mechanism (e.g., a custom database, an in-memory store for
/// testing, or an external API), implement this interface and register the implementation with
/// the ASP.NET Core service provider.
/// </para>
/// <para>
/// See <see href="https://docs.duendesoftware.com/identityserver/reference/stores/client-configuration-store">Client Configuration Store</see>
/// in the IdentityServer documentation for more details.
/// </para>
/// </remarks>
public interface IClientConfigurationStore
{
    /// <summary>
    /// Adds a newly registered client to the configuration store, making it available for
    /// subsequent authorization requests.
    /// </summary>
    /// <param name="client">The client model to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task AddAsync(Client client, Ct ct);
}
