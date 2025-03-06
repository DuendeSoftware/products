// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// User session store
/// </summary>
public interface IServerSideSessionStore
{
    /// <summary>
    /// Retrieves a session
    /// </summary>
    Task<ServerSideSession?> GetSessionAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a session
    /// </summary>
    Task CreateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a session
    /// </summary>
    Task UpdateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session
    /// </summary>
    Task DeleteSessionAsync(string key, CancellationToken cancellationToken = default);


    /// <summary>
    /// Gets sessions for a specific subject id and/or session id
    /// </summary>
    Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes sessions for a specific subject id and/or session id
    /// </summary>
    Task DeleteSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default);


    /// <summary>
    /// Removes and returns expired sessions
    /// </summary>
    Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default);


    /// <summary>
    /// Queries sessions based on filter
    /// </summary>
    Task<QueryResult<ServerSideSession>> QuerySessionsAsync(SessionQuery? filter = null, CancellationToken cancellationToken = default);
}
