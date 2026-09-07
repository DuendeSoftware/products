// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

internal class StubSessionCoordinationService : ISessionCoordinationService
{
    public List<UserSession> ProcessedSessions { get; } = [];
    public Exception? ThrowOnProcess { get; set; }

    public Task ProcessExpirationAsync(UserSession session, Ct _)
    {
        if (ThrowOnProcess is not null)
        {
            throw ThrowOnProcess;
        }

        ProcessedSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task ProcessLogoutAsync(UserSession session, Ct _) => Task.CompletedTask;

    public Task<bool> ValidateSessionAsync(SessionValidationRequest request, Ct _) => Task.FromResult(true);
}
