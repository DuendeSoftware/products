// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockLogoutNotificationService : ILogoutNotificationService
{
    public bool GetFrontChannelLogoutNotificationsUrlsCalled { get; set; }
    public List<string> FrontChannelLogoutNotificationsUrls { get; set; } = new List<string>();

    public bool SendBackChannelLogoutNotificationsCalled { get; set; }
    public List<BackChannelLogoutRequest> BackChannelLogoutRequests { get; set; } = new List<BackChannelLogoutRequest>();

    public Task<IEnumerable<string>> GetFrontChannelLogoutNotificationsUrlsAsync(LogoutNotificationContext context, Ct _)
    {
        GetFrontChannelLogoutNotificationsUrlsCalled = true;
        return Task.FromResult(FrontChannelLogoutNotificationsUrls.AsEnumerable());
    }

    public Task<IEnumerable<BackChannelLogoutRequest>> GetBackChannelLogoutNotificationsAsync(LogoutNotificationContext context, Ct _)
    {
        SendBackChannelLogoutNotificationsCalled = true;
        return Task.FromResult(BackChannelLogoutRequests.AsEnumerable());
    }
}
