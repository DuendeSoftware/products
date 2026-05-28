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
    public IReadOnlyCollection<string> ClientIds { get; set; } = default!;

    /// <summary>
    /// The SAML Service Provider sessions that the user has authenticated to.
    /// Contains full session data including NameId, SessionIndex, and NameIdFormat
    /// required to construct logout requests.
    /// </summary>
    public IReadOnlyCollection<SamlSpSessionData> SamlSessions { get; set; } = [];

    /// <summary>
    /// The EntityId of the SAML Service Provider that initiated the logout request, if any.
    /// This SP should be excluded from front-channel logout notifications because it will
    /// receive a LogoutResponse instead.
    /// </summary>
    public string? SamlInitiatingServiceProviderEntityId { get; set; }

    /// <summary>
    /// Indicates why the user's session ended, if known.
    /// </summary>
    public LogoutNotificationReason? LogoutReason { get; set; }

    /// <summary>
    /// The logout ID that correlates this context to the SAML logout session store.
    /// Set only for SAML-initiated logouts; <see langword="null"/> for OIDC-initiated logouts.
    /// </summary>
    public string? SamlLogoutId { get; set; }
}
