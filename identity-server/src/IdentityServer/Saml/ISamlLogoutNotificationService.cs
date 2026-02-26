// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml;

public interface ISamlLogoutNotificationService
{
    /// <summary>
    /// Builds the URLs needed for front-channel logout notification.
    /// </summary>
    /// <param name="context">The context for the logout notification.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<IEnumerable<ISamlFrontChannelLogout>> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context, Ct ct);
}
