// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml;

public interface ISamlLogoutNotificationService
{
    Task<IEnumerable<ISamlFrontChannelLogout>> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context);
}
