// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.AspNetIdentity;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.UnitTests.AspNetIdentity;

public class DefaultSessionClaimsFilterTests
{
    [Fact]
    public async Task FilterToSessionClaimsAsync_with_session_and_non_session_claims_should_filter_to_only_session_claims()
    {
        var claims = new[]
        {
            new Claim(JwtClaimTypes.AuthenticationMethod, "pwd"),
            new Claim(JwtClaimTypes.IdentityProvider, "idp"),
            new Claim(JwtClaimTypes.AuthenticationTime, "123456"),
            new Claim("custom", "value"),
            new Claim(ClaimTypes.Name, "bob")
        };
        var currentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        Claim[] newClaims = [new Claim("custom", "value"), new Claim(ClaimTypes.Name, "bob")];
        var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity(newClaims));
        var filter = new DefaultSessionClaimsFilter();
        var context = new SecurityStampRefreshingPrincipalContext() { NewPrincipal = newPrincipal, CurrentPrincipal = currentPrincipal };

        var result = await filter.FilterToSessionClaimsAsync(context);

        var resultTypes = result.Select(c => c.Type).ToList();
        resultTypes.Count.ShouldBe(3);
        resultTypes.ShouldContain(JwtClaimTypes.AuthenticationMethod);
        resultTypes.ShouldContain(JwtClaimTypes.IdentityProvider);
        resultTypes.ShouldContain(JwtClaimTypes.AuthenticationTime);
        resultTypes.ShouldNotContain("custom");
        resultTypes.ShouldNotContain(ClaimTypes.Name);

        currentPrincipal.Claims.Count().ShouldBe(claims.Length);
        newPrincipal.Claims.Count().ShouldBe(newClaims.Length);
    }

    [Fact]
    public async Task FilterToSessionClaimsAsync_with_only_session_claims_should_filter_to_session_claims()
    {
        var claims = new[]
        {
            new Claim(JwtClaimTypes.AuthenticationMethod, "pwd"),
            new Claim(JwtClaimTypes.IdentityProvider, "idp"),
            new Claim(JwtClaimTypes.AuthenticationTime, "123456")
        };
        var currentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var filter = new DefaultSessionClaimsFilter();
        var context = new SecurityStampRefreshingPrincipalContext { NewPrincipal = newPrincipal, CurrentPrincipal = currentPrincipal };

        var result = await filter.FilterToSessionClaimsAsync(context);

        result.Count.ShouldBe(3);
        string[] expectClaimTypes = [
            JwtClaimTypes.AuthenticationMethod,
            JwtClaimTypes.IdentityProvider,
            JwtClaimTypes.AuthenticationTime
        ];
        result.ShouldAllBe(c => expectClaimTypes.Contains(c.Type));
        currentPrincipal.Claims.Count().ShouldBe(claims.Length);
        newPrincipal.Claims.Count().ShouldBe(0);
    }

    [Fact]
    public async Task FilterToSessionClaimsAsync_with_no_session_claims_should_return_empty()
    {
        var claims = new[]
        {
            new Claim("custom", "value"),
            new Claim(ClaimTypes.Name, "bob")
        };
        var currentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var filter = new DefaultSessionClaimsFilter();
        var context = new SecurityStampRefreshingPrincipalContext { NewPrincipal = newPrincipal, CurrentPrincipal = currentPrincipal };

        var result = await filter.FilterToSessionClaimsAsync(context);

        result.ShouldBeEmpty();
        currentPrincipal.Claims.Count().ShouldBe(claims.Length);
        newPrincipal.Claims.Count().ShouldBe(claims.Length);
    }

    [Fact]
    public async Task FilterToSessionClaimsAsync_when_principal_has_no_claims_should_return_empty()
    {
        var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var currentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var filter = new DefaultSessionClaimsFilter();
        var context = new SecurityStampRefreshingPrincipalContext { NewPrincipal = newPrincipal, CurrentPrincipal = currentPrincipal };

        var result = await filter.FilterToSessionClaimsAsync(context);

        result.ShouldBeEmpty();
        currentPrincipal.Claims.Count().ShouldBe(0);
        newPrincipal.Claims.Count().ShouldBe(0);
    }
}
