// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.EntityFramework;

/// <summary>
/// Service that cleans up persisted grants and device codes that are no longer
/// needed.
/// </summary>
public interface ITokenCleanupService
{
    /// <summary>
    /// Removes expired persisted grants, expired device codes, and optionally
    /// consumed persisted grants from the stores.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task CleanupGrantsAsync(Ct ct);
}
