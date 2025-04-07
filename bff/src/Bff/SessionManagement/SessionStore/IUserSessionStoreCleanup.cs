// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// User session store cleanup
/// </summary>
public interface IUserSessionStoreCleanup
{
    /// <summary>
    /// Deletes expired sessions
    /// </summary>
    Task<int> DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default);
}
