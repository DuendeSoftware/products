// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Duende.Bff;

/// <summary>
/// User session store
/// </summary>
public interface IUserSessionStore
{
    /// <summary>
    /// Retrieves a user session
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task<UserSession?> GetUserSessionAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a user session
    /// </summary>
    /// <param name="session"></param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task CreateUserSessionAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user session
    /// </summary>
    /// <param name="key"></param>
    /// <param name="session"></param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task UpdateUserSessionAsync(string key, UserSessionUpdate session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user session
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task DeleteUserSessionAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries user sessions based on the filter.
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task<IReadOnlyCollection<UserSession>> GetUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes user sessions based on the filter.
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns></returns>
    Task DeleteUserSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default);
}