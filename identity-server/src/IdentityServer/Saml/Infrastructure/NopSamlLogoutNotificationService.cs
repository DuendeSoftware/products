// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Services;

namespace Duende.IdentityServer.Saml.Infrastructure;

internal class NopSamlLogoutNotificationService : ISamlLogoutNotificationService
{
    private static readonly SamlLogoutNotificationResult EmptyResult = new(Array.Empty<SamlLogoutRequestContext>(), 0);

    public Task<SamlLogoutNotificationResult> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context, Ct ct) =>
        Task.FromResult(EmptyResult);
}
