// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.Internal.Saml;

internal class NopSamlLogoutNotificationService : ISamlLogoutNotificationService
{
    public Task<IEnumerable<ISamlFrontChannelLogout>> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context) =>
        Task.FromResult(Enumerable.Empty<ISamlFrontChannelLogout>());
}
