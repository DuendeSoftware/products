// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extension methods for determining client session coordination.
/// </summary>
internal static class ClientSessionCoordinationExtensions
{
    /// <summary>
    /// Returns true if the client's lifetime should be coordinated with the user session.
    /// The client-level value wins; otherwise the global option is used.
    /// </summary>
    internal static bool ShouldCoordinateLifetimeWithUserSession(this Client client, IdentityServerOptions options) =>
        client.CoordinateLifetimeWithUserSession == true ||
        (options.Authentication.CoordinateClientLifetimesWithUserSession && client.CoordinateLifetimeWithUserSession != false);
}
