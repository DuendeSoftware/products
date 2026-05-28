// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Services;

namespace UnitTests.Common;

public class MockSamlLogoutNotificationService : ISamlLogoutNotificationService
{
    public bool GetSamlFrontChannelLogoutsAsyncCalled { get; set; }
    public List<SamlLogoutRequestContext> SamlFrontChannelLogouts { get; set; } = [];
    public int SkippedCount { get; set; }

    public Task<SamlLogoutNotificationResult> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context, Ct _)
    {
        GetSamlFrontChannelLogoutsAsyncCalled = true;
        return Task.FromResult(new SamlLogoutNotificationResult(SamlFrontChannelLogouts, SkippedCount));
    }
}
