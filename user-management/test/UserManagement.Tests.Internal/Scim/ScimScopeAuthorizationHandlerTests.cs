// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.UserManagement.Scim.Internal;
using Microsoft.AspNetCore.Authorization;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimScopeAuthorizationHandlerTests
{
    private readonly ScimScopeAuthorizationHandler _sut = new();

    [Fact]
    public async Task SucceedsWhenSingleScopeClaimMatches()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("scope", "scim"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task SucceedsWhenOneOfMultipleScopeClaimsMatches()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("scope", "openid"),
            new Claim("scope", "profile"),
            new Claim("scope", "scim"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task SucceedsWhenSpaceDelimitedScopeContainsMatch()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("scope", "openid profile scim"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task SucceedsWhenAnyRequiredScopeMatches()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim", "scim.read"),
            new Claim("scope", "scim.read"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task DoesNotSucceedWhenNoScopeClaims()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task DoesNotSucceedWhenScopeDoesNotMatch()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("scope", "openid"),
            new Claim("scope", "profile"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task DoesNotSucceedWhenSpaceDelimitedScopeDoesNotContainMatch()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("scope", "openid profile email"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ScopeComparisonIsCaseSensitive()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("scope", "SCIM"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandlesEmptyClaimValue()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("scope", ""));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandlesMixedClaimFormats()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim.read"),
            new Claim("scope", "openid profile"),
            new Claim("scope", "scim.read"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task IgnoresNonScopeClaims()
    {
        var context = CreateContext(
            new ScimScopeRequirement("scim"),
            new Claim("role", "scim"),
            new Claim("aud", "scim"));

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task SucceedsWithUnauthenticatedIdentityWhenScopeClaimPresent()
    {
        // The handler checks claims regardless of authentication state.
        // It is the policy's AddAuthenticationSchemes that gates authentication,
        // not the handler itself. So the handler succeeds even when the identity
        // is not authenticated — this is the expected behavior.
        var identity = new ClaimsIdentity(); // no authenticationType → IsAuthenticated == false
        identity.AddClaim(new Claim("scope", "scim"));
        var principal = new ClaimsPrincipal(identity);
        var context = new AuthorizationHandlerContext(
            [new ScimScopeRequirement("scim")], principal, resource: null);

        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    private static AuthorizationHandlerContext CreateContext(
        ScimScopeRequirement requirement,
        params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext([requirement], principal, resource: null);
    }
}
