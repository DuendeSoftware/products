// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Common;

internal class MockAuthenticationService : IAuthenticationService
{
    public AuthenticateResult Result { get; set; }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme) => Task.FromResult(Result);

    public Task ChallengeAsync(HttpContext context, string scheme, AuthenticationProperties properties) => Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string scheme, AuthenticationProperties properties) => Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal, AuthenticationProperties properties) => Task.CompletedTask;

    public Task SignOutAsync(HttpContext context, string scheme, AuthenticationProperties properties) => Task.CompletedTask;
}
