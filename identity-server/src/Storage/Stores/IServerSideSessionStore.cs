// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves server-side user authentication session data. When the
/// server-side sessions feature is enabled, IdentityServer stores the authentication
/// ticket in this store rather than solely in the browser cookie. This allows sessions
/// to be centrally managed, queried, and revoked. Implement this interface to persist
/// session data in any backing store.
/// </summary>
public interface IServerSideSessionStore
{
    /// <summary>
    /// Retrieves a single session by its unique key.
    /// </summary>
    /// <param name="key">The unique key that identifies the session.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="ServerSideSession"/> with the specified <paramref name="key"/>,
    /// or <see langword="null"/> if no matching session exists.
    /// </returns>
    Task<ServerSideSession?> GetSessionAsync(string key, Ct ct);

    /// <summary>
    /// Persists a new session record in the store.
    /// </summary>
    /// <param name="session">The session to create.</param>
    /// <param name="ct">The cancellation token.</param>
    Task CreateSessionAsync(ServerSideSession session, Ct ct);

    /// <summary>
    /// Replaces the stored data for an existing session with the provided values.
    /// </summary>
    /// <param name="session">The session containing updated data to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpdateSessionAsync(ServerSideSession session, Ct ct);

    /// <summary>
    /// Removes the session identified by the specified key.
    /// </summary>
    /// <param name="key">The unique key of the session to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    Task DeleteSessionAsync(string key, Ct ct);


    /// <summary>
    /// Gets all sessions that match the specified filter. The filter can constrain
    /// results by subject ID and/or session ID.
    /// </summary>
    /// <param name="filter">
    /// A <see cref="SessionFilter"/> specifying the subject ID and/or session ID to
    /// match. At least one property must be set.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="ServerSideSession"/> objects that match
    /// the filter. Returns an empty collection when no sessions match.
    /// </returns>
    Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, Ct ct);

    /// <summary>
    /// Removes all sessions that match the specified filter. The filter can constrain
    /// the deletion by subject ID and/or session ID.
    /// </summary>
    /// <param name="filter">
    /// A <see cref="SessionFilter"/> specifying the subject ID and/or session ID to
    /// match. At least one property must be set.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task DeleteSessionsAsync(SessionFilter filter, Ct ct);


    /// <summary>
    /// Atomically removes and returns sessions that have passed their expiration time.
    /// This is used by the session cleanup background service to purge stale sessions.
    /// </summary>
    /// <param name="count">The maximum number of expired sessions to remove and return.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of the <see cref="ServerSideSession"/> objects that were
    /// removed. Returns an empty collection when no expired sessions exist.
    /// </returns>
    Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, Ct ct);


    /// <summary>
    /// Queries sessions using a paginated filter. Supports cursor-based pagination and
    /// optional filtering by subject ID, session ID, and display name.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="filter">
    /// An optional <see cref="SessionQuery"/> that controls pagination (via
    /// <c>ResultsToken</c> and <c>CountRequested</c>) and filtering (by subject ID,
    /// session ID, or display name). When <see langword="null"/>, the first page of
    /// all sessions is returned.
    /// </param>
    /// <returns>
    /// A <see cref="QueryResult{T}"/> containing the matching <see cref="ServerSideSession"/>
    /// objects for the current page, along with pagination metadata such as
    /// <c>HasNextResults</c>, <c>HasPrevResults</c>, and <c>ResultsToken</c>.
    /// </returns>
    Task<QueryResult<ServerSideSession>> QuerySessionsAsync(Ct ct, SessionQuery? filter = null);
}
