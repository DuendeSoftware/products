// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Configures the behavior for server-side sessions.
/// </summary>
public class ServerSideSessionOptions
{
    /// <summary>
    /// The claim type used for the user's display name.
    /// </summary>
    public string? UserDisplayNameClaimType { get; set; }

    /// <summary>
    /// If enabled, will perodically cleanup expired sessions.
    /// </summary>
    public bool RemoveExpiredSessions { get; set; } = true;

    /// <summary>
    /// If enabled, when server-side sessions are removed due to expiration, will back-channel logout notifications be sent.
    /// This will, in effect, tie a user's session lifetime at a client to their session lifetime at IdentityServer.
    /// </summary>
    public bool ExpiredSessionsTriggerBackchannelLogout { get; set; }

    /// <summary>
    /// Frequency expired sessions will be removed.
    /// </summary>
    public TimeSpan RemoveExpiredSessionsFrequency { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// If multiple nodes are running the server side session cleaup at the same time, there will be
    /// concurrency issues in the database updates. To reduce the rsk, the startup time
    /// of the first run can be fuzzed (randomized). The default is <c>true</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> if startup time should be fuzzed, otherwise false.
    /// </value>
    public bool FuzzExpiredSessionRemovalStart { get; set; } = true;

    /// <summary>
    /// Number of expired sessions records to be removed at a time.
    /// </summary>
    public int RemoveExpiredSessionsBatchSize { get; set; } = 100;
}
