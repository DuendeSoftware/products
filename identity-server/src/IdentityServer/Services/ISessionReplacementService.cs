// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Services;

/// <summary>
/// Service invoked by the server-side ticket store when an existing server-side session
/// record (same key) is renewed with a different subject id and/or session id, i.e. one
/// user session replaces another. Implementations should clean up state associated with
/// the previous session. This is not a logout.
/// </summary>
public interface ISessionReplacementService
{
    /// <summary>
    /// Processes a session replacement.
    /// </summary>
    /// <param name="context">The context describing the previous and new session.</param>
    /// <param name="ct">The cancellation token.</param>
    Task ProcessReplacementAsync(SessionReplacementContext context, Ct ct);
}
