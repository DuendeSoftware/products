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
    Task<ServerSideSession?> GetSessionAsync(string key, CT ct = default);

    /// <summary>
    /// Creates a session
    /// </summary>
    Task CreateSessionAsync(ServerSideSession session, CT ct = default);

    /// <summary>
    /// Updates a session
    /// </summary>
    Task UpdateSessionAsync(ServerSideSession session, CT ct = default);

    /// <summary>
    /// Deletes a session
    /// </summary>
    Task DeleteSessionAsync(string key, CT ct = default);


    /// <summary>
    /// Gets sessions for a specific subject id and/or session id
    /// </summary>
    Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CT ct = default);

    /// <summary>
    /// Deletes sessions for a specific subject id and/or session id
    /// </summary>
    Task DeleteSessionsAsync(SessionFilter filter, CT ct = default);


    /// <summary>
    /// Removes and returns expired sessions
    /// </summary>
    Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CT ct = default);


    /// <summary>
    /// Queries sessions based on filter
    /// </summary>
    Task<QueryResult<ServerSideSession>> QuerySessionsAsync(SessionQuery? filter = null, CT ct = default);
}
