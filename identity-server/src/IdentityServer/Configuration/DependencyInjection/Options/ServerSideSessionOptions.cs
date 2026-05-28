// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for server-side session storage, including periodic cleanup of expired sessions
/// and back-channel logout integration.
/// </summary>
public class ServerSideSessionOptions
{
    /// <summary>
    /// Gets or sets the claim type used to populate the display name shown for a user's session in
    /// management UIs.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>null</c> (unset) due to potential PII concerns. Common values are
    /// <c>JwtClaimTypes.Name</c>, <c>JwtClaimTypes.Email</c>, or a custom claim type.
    /// </remarks>
    public string? UserDisplayNameClaimType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a background job that periodically removes expired server-side sessions is enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. The cleanup frequency is controlled by
    /// <see cref="RemoveExpiredSessionsFrequency"/>.
    /// </remarks>
    public bool RemoveExpiredSessions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether back-channel logout notifications are sent to clients when server-side sessions are removed
    /// due to expiration.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When enabled, expiring a server-side session effectively ties
    /// the user's session lifetime at each client to their session lifetime at IdentityServer.
    /// </remarks>
    public bool ExpiredSessionsTriggerBackchannelLogout { get; set; }

    /// <summary>
    /// Gets or sets how often the background job runs to remove expired server-side sessions.
    /// </summary>
    /// <remarks>
    /// Defaults to 10 minutes. Only relevant when <see cref="RemoveExpiredSessions"/> is
    /// <c>true</c>.
    /// </remarks>
    public TimeSpan RemoveExpiredSessionsFrequency { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets a value indicating whether the initial start time of the expired-session cleanup job is randomized to reduce the
    /// likelihood of concurrent cleanup conflicts when multiple server instances are running.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. When enabled, the first cleanup run is scheduled at a random
    /// time between host startup and the first <see cref="RemoveExpiredSessionsFrequency"/>
    /// interval. Subsequent runs follow the configured frequency.
    /// </remarks>
    public bool FuzzExpiredSessionRemovalStart { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of expired session records deleted in a single cleanup batch.
    /// </summary>
    /// <remarks>
    /// Defaults to 100. Tune this value to balance cleanup throughput against database load.
    /// </remarks>
    public int RemoveExpiredSessionsBatchSize { get; set; } = 100;
}
