// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Services;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Service for building SAML front-channel logout notifications.
/// </summary>
public interface ISamlLogoutNotificationService
{
    /// <summary>
    /// Builds the SAML messages needed for front-channel logout notification.
    /// </summary>
    /// <param name="context">The context for the logout notification.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<SamlLogoutNotificationResult> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context, Ct ct);
}

/// <summary>
/// Result of generating SAML front-channel logout notifications.
/// </summary>
/// <param name="Messages">The successfully generated logout request contexts.</param>
/// <param name="SkippedCount">The number of SPs that could not be notified.</param>
public sealed record SamlLogoutNotificationResult(
    IReadOnlyCollection<SamlLogoutRequestContext> Messages,
    int SkippedCount);
