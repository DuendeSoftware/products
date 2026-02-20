// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace Duende.IdentityServer.EntityFramework.Interfaces;

/// <summary>
/// Abstraction for the operational data context.
/// </summary>
/// <seealso cref="System.IDisposable" />
public interface IPersistedGrantDbContext : IDisposable
{
    /// <summary>
    /// Gets or sets the persisted grants.
    /// </summary>
    /// <value>
    /// The persisted grants.
    /// </value>
    DbSet<PersistedGrant> PersistedGrants { get; set; }

    /// <summary>
    /// Gets or sets the device flow codes.
    /// </summary>
    /// <value>
    /// The device flow codes.
    /// </value>
    DbSet<DeviceFlowCodes> DeviceFlowCodes { get; set; }

    /// <summary>
    /// Gets or sets the keys.
    /// </summary>
    /// <value>
    /// The keys.
    /// </value>
    DbSet<Key> Keys { get; set; }

    /// <summary>
    /// Gets or sets the user sessions.
    /// </summary>
    /// <value>
    /// The server side sessions.
    /// </value>
    DbSet<ServerSideSession> ServerSideSessions { get; set; }

    /// <summary>
    /// Gets or sets the pushed authorization requests.
    /// </summary>
    /// <value>
    /// The pushed authorization requests.
    /// </value>
    DbSet<PushedAuthorizationRequest> PushedAuthorizationRequests { get; set; }

    /// <summary>
    /// Saves the changes.
    /// </summary>
    /// <returns></returns>
    Task<int> SaveChangesAsync(CT ct);

    // this is here only because of this: https://github.com/DuendeSoftware/IdentityServer/issues/472
    // and because Microsoft implements the old API explicitly: https://github.com/dotnet/aspnetcore/blob/v6.0.0-rc.2.21480.10/src/Identity/ApiAuthorization.IdentityServer/src/Data/ApiAuthorizationDbContext.cs

    /// <summary>
    /// Saves the changes.
    /// </summary>
    /// <returns></returns>
    Task<int> SaveChangesAsync() => SaveChangesAsync(CT.None);
}
