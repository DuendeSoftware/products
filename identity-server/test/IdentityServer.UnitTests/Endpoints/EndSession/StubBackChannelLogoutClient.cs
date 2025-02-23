// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Endpoints.EndSession;

internal class StubBackChannelLogoutClient : IBackChannelLogoutService
{
    public bool SendLogoutsWasCalled { get; set; }

    public Task SendLogoutNotificationsAsync(LogoutNotificationContext context)
    {
        SendLogoutsWasCalled = true;
        return Task.CompletedTask;
    }
}