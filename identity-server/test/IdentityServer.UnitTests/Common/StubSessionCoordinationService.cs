// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

internal class StubSessionCoordinationService : ISessionCoordinationService
{
    public Task ProcessExpirationAsync(UserSession session) => Task.CompletedTask;

    public Task ProcessLogoutAsync(UserSession session) => Task.CompletedTask;

    public Task<bool> ValidateSessionAsync(SessionValidationRequest request) => Task.FromResult(true);
}
