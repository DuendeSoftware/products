// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

internal class MockSessionReplacementService : ISessionReplacementService
{
    public List<SessionReplacementContext> Calls { get; } = [];

    public Task ProcessReplacementAsync(SessionReplacementContext context, Ct ct)
    {
        Calls.Add(context);
        return Task.CompletedTask;
    }
}
