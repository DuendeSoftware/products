// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Provides the context necessary to construct a logout notification.
/// </summary>
public class LogoutNotificationContext
{
    /// <summary>
    ///  The SubjectId of the user.
    /// </summary>
    public string SubjectId { get; set; } = default!;

    /// <summary>
    /// The session Id of the user's authentication session.
    /// </summary>
    public string SessionId { get; set; } = default!;

    /// <summary>
    /// The issuer for the back-channel logout
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// The list of client Ids that the user has authenticated to.
    /// </summary>
    public IEnumerable<string> ClientIds { get; set; } = default!;

    /// <summary>
    /// The SAML Service Provider sessions that the user has authenticated to.
    /// Contains full session data including NameId, SessionIndex, and NameIdFormat
    /// required to construct logout requests.
    /// </summary>
    public IEnumerable<SamlSpSessionData> SamlSessions { get; set; } = [];

    /// <summary>
    /// Indicates why the user's session ended, if known.
    /// </summary>
    public LogoutNotificationReason? LogoutReason { get; set; }
}
