// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

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
