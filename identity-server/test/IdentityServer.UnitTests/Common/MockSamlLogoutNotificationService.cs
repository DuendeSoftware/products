// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;

namespace UnitTests.Common;

public class MockSamlLogoutNotificationService : ISamlLogoutNotificationService
{
    public bool GetSamlFrontChannelLogoutsAsyncCalled { get; set; }
    public List<ISamlFrontChannelLogout> SamlFrontChannelLogouts { get; set; } = [];

    public Task<IEnumerable<ISamlFrontChannelLogout>> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context)
    {
        GetSamlFrontChannelLogoutsAsyncCalled = true;
        return Task.FromResult(SamlFrontChannelLogouts.AsEnumerable());
    }
}
