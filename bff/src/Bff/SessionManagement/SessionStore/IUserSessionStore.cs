// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// User session store
/// </summary>
public interface IUserSessionStore
{
    /// <summary>
    /// Retrieves a user session
    /// </summary>
    /// <param name="key"></param>
    /// <param name="ct">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task<UserSession?> GetUserSessionAsync(UserSessionKey key, CT ct = default);

    /// <summary>
    /// Creates a user session
    /// </summary>
    /// <param name="session"></param>
    /// <param name="ct">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task CreateUserSessionAsync(UserSession session, CT ct = default);

    /// <summary>
    /// Updates a user session
    /// </summary>
    /// <param name="key"></param>
    /// <param name="session"></param>
    /// <param name="ct">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task UpdateUserSessionAsync(UserSessionKey key, UserSessionUpdate session, CT ct = default);

    /// <summary>
    /// Deletes a user session
    /// </summary>
    /// <param name="key"></param>
    /// <param name="ct">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task DeleteUserSessionAsync(UserSessionKey key, CT ct = default);

    /// <summary>
    /// Queries user sessions based on the filter.
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="ct">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task<IReadOnlyCollection<UserSession>> GetUserSessionsAsync(UserSessionsFilter filter, CT ct = default);

    /// <summary>
    /// Deletes user sessions based on the filter.
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="ct">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task DeleteUserSessionsAsync(UserSessionsFilter filter, CT ct = default);
}
