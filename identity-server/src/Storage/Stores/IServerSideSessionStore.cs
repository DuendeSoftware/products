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
    /// <param name="key">The session key.</param>
    /// <param name="ct">The <see cref="CT"/>.</param>
    Task<ServerSideSession?> GetSessionAsync(string key, CT ct);

    /// <summary>
    /// Creates a session
    /// </summary>
    /// <param name="session">The session to create.</param>
    /// <param name="ct">The <see cref="CT"/>.</param>
    Task CreateSessionAsync(ServerSideSession session, CT ct);

    /// <summary>
    /// Updates a session
    /// </summary>
    /// <param name="session">The session to update.</param>
    /// <param name="ct">The <see cref="CT"/>.</param>
    Task UpdateSessionAsync(ServerSideSession session, CT ct);

    /// <summary>
    /// Deletes a session
    /// </summary>
    /// <param name="key">The session key.</param>
    /// <param name="ct">The <see cref="CT"/>.</param>
    Task DeleteSessionAsync(string key, CT ct);


    /// <summary>
    /// Gets sessions for a specific subject id and/or session id
    /// </summary>
    /// <param name="filter">The session filter.</param>
    /// <param name="ct">The <see cref="CT"/>.</param>
    Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CT ct);

    /// <summary>
    /// Deletes sessions for a specific subject id and/or session id
    /// </summary>
    /// <param name="filter">The session filter.</param>
    /// <param name="ct">The <see cref="CT"/>.</param>
    Task DeleteSessionsAsync(SessionFilter filter, CT ct);


    /// <summary>
    /// Removes and returns expired sessions
    /// </summary>
    /// <param name="count">The maximum number of sessions to return.</param>
    /// <param name="ct">The <see cref="CT"/>.</param>
    Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CT ct);


    /// <summary>
    /// Queries sessions based on filter
    /// </summary>
    /// <param name="ct">The <see cref="CT"/>.</param>
    /// <param name="filter">The session query filter.</param>
    Task<QueryResult<ServerSideSession>> QuerySessionsAsync(CT ct, SessionQuery? filter = null);
}
