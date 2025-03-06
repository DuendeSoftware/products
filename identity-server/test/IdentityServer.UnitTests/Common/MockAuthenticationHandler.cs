// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Common;

internal class MockAuthenticationHandler : IAuthenticationHandler
{
    public AuthenticateResult Result { get; set; } = AuthenticateResult.NoResult();

    public Task<AuthenticateResult> AuthenticateAsync()
    {
        return Task.FromResult(Result);
    }

    public Task ChallengeAsync(AuthenticationProperties properties)
    {
        return Task.CompletedTask;
    }

    public Task ForbidAsync(AuthenticationProperties properties)
    {
        return Task.CompletedTask;
    }

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
    {
        return Task.CompletedTask;
    }
}