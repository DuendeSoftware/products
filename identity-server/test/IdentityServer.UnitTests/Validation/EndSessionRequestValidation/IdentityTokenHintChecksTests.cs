// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using UnitTests.Common;
using Xunit;

namespace UnitTests.Validation.EndSessionRequestValidation;

public class IdentityTokenHintChecksTests
{
    private const string Category = "id_token_hint checks";

    private static Duende.IdentityServer.Validation.TokenValidationResult CreateResult(IEnumerable<Claim> claims) =>
        new()
        {
            IsError = false,
            Claims = claims
        };

    private static IEnumerable<Claim> ReadClaims(string jwt) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims;

    private static string CreateJwtWithTyp(string? typ, bool includeSub = true)
    {
        var claims = includeSub
            ? new Dictionary<string, object> { { JwtClaimTypes.Subject, "valid" } }
            : new Dictionary<string, object>();

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://idsvr.com",
            Audience = "roclient",
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = TestCert.LoadSigningCredentials(),
            TokenType = typ,
            Claims = claims
        });
    }

    private static string CreateJwtWithTypAndClaims(string? typ, IDictionary<string, object> claims) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://idsvr.com",
            Audience = "roclient",
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = TestCert.LoadSigningCredentials(),
            TokenType = typ,
            Claims = claims
        });

    // The JWT whose header is explicitly verified below to have no typ header, built the same
    // way as the real identity token, so the no-typ test exercises the actual header parsing
    // path rather than relying on JsonWebTokenHandler.CreateToken's handling of a null TokenType.
    private static string CreateJwtWithoutTypHeader(IEnumerable<Claim> claims)
    {
        var signingCredentials = TestCert.LoadSigningCredentials();
        var now = DateTime.UtcNow;

        var header = new JwtHeader(signingCredentials);
        header.Remove("typ");
        var payload = new JwtPayload(
            issuer: "https://idsvr.com",
            audience: "roclient",
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(10),
            issuedAt: now);

        var token = new JwtSecurityToken(header, payload);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Header.Typ.ShouldBeNull();

        return jwt;
    }

    public static TheoryData<string> NonIdTokenTypes() => new()
    {
        new IdentityServerOptions().AccessTokenJwtType,
        new IdentityServerOptions().LogoutTokenJwtType,
        "token-introspection+jwt",
    };

    [Theory]
    [Trait("Category", Category)]
    [MemberData(nameof(NonIdTokenTypes))]
    public void hint_with_non_id_token_type_should_fail(string jwtType)
    {
        var jwt = CreateJwtWithTyp(jwtType);
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_id_token_type_should_succeed()
    {
        var jwt = CreateJwtWithTyp("JWT");
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_without_typ_header_and_with_sub_should_succeed()
    {
        var claims = new[] { new Claim(JwtClaimTypes.Subject, "valid") };
        var jwt = CreateJwtWithoutTypHeader(claims);
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_without_sub_should_fail()
    {
        var jwt = CreateJwtWithTyp("JWT", includeSub: false);
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.NoSubClaimReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void unparsable_hint_should_be_rejected()
    {
        var options = TestIdentityServerOptions.Create();
        var claims = new[] { new Claim(JwtClaimTypes.Subject, "valid") };

        var failure = IdentityTokenHintChecks.Validate("not.a.jwt", CreateResult(claims), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnparsableReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_jwt_set_to_null_on_the_result_is_still_checked_against_the_submitted_hint()
    {
        // A custom or decorating ITokenValidator may not populate TokenValidationResult.Jwt. The
        // typ check must still read the typ header from the token as submitted, not accept it
        // just because the result carries no Jwt to check.
        var jwt = CreateJwtWithTyp("at+jwt");
        var options = TestIdentityServerOptions.Create();

        var resultWithoutJwt = new Duende.IdentityServer.Validation.TokenValidationResult
        {
            IsError = false,
            Jwt = null,
            Claims = ReadClaims(jwt)
        };

        var failure = IdentityTokenHintChecks.Validate(jwt, resultWithoutJwt, options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_custom_typ_should_fail_by_default()
    {
        var jwt = CreateJwtWithTyp("id_token+jwt");
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_custom_typ_should_succeed_when_configured()
    {
        var jwt = CreateJwtWithTyp("id_token+jwt");
        var options = TestIdentityServerOptions.Create();
        options.IdentityTokenJwtTypes.Add("id_token+jwt");

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_access_token_typ_listed_in_IdentityTokenJwtTypes_should_still_fail()
    {
        var jwt = CreateJwtWithTyp("at+jwt");
        var options = TestIdentityServerOptions.Create();
        options.IdentityTokenJwtTypes.Add("at+jwt");

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_custom_access_token_typ_listed_in_IdentityTokenJwtTypes_should_still_fail()
    {
        var jwt = CreateJwtWithTyp("custom-at+jwt");
        var options = TestIdentityServerOptions.Create();
        options.AccessTokenJwtType = "custom-at+jwt";
        options.IdentityTokenJwtTypes.Add("custom-at+jwt");

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_custom_logout_token_typ_listed_in_IdentityTokenJwtTypes_should_still_fail()
    {
        var jwt = CreateJwtWithTyp("custom-logout+jwt");
        var options = TestIdentityServerOptions.Create();
        options.LogoutTokenJwtType = "custom-logout+jwt";
        options.IdentityTokenJwtTypes.Add("custom-logout+jwt");

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    public static TheoryData<string> ReservedDefaultTypes() => new()
    {
        "logout+jwt",
        "token-introspection+jwt",
    };

    [Theory]
    [Trait("Category", Category)]
    [MemberData(nameof(ReservedDefaultTypes))]
    public void hint_with_reserved_default_typ_listed_in_IdentityTokenJwtTypes_should_still_fail(string jwtType)
    {
        var jwt = CreateJwtWithTyp(jwtType);
        var options = TestIdentityServerOptions.Create();
        options.IdentityTokenJwtTypes.Add(jwtType);

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("jwt")]
    [InlineData("Jwt")]
    public void hint_with_differently_cased_generic_jwt_typ_should_succeed(string typ)
    {
        var jwt = CreateJwtWithTyp(typ);
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("AT+JWT")]
    [InlineData("Logout+JWT")]
    [InlineData("TOKEN-INTROSPECTION+JWT")]
    public void hint_with_differently_cased_reserved_typ_should_fail(string typ)
    {
        var jwt = CreateJwtWithTyp(typ);
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.UnexpectedTypeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_differently_cased_custom_typ_should_succeed_when_configured()
    {
        var jwt = CreateJwtWithTyp("ID_TOKEN+JWT");
        var options = TestIdentityServerOptions.Create();
        options.IdentityTokenJwtTypes.Add("id_token+jwt");

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("access")]
    [InlineData("logout")]
    public void hint_should_succeed_when_other_token_type_is_set_to_JWT(string setting)
    {
        // Older deployments set AccessTokenJwtType or LogoutTokenJwtType to the generic "JWT".
        // Identity tokens use the same generic type, so they must still be accepted.
        var jwt = CreateJwtWithTyp("JWT");
        var options = TestIdentityServerOptions.Create();
        if (setting == "access")
        {
            options.AccessTokenJwtType = "JWT";
        }
        else
        {
            options.LogoutTokenJwtType = "JWT";
        }

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_typ_jwt_and_backchannel_logout_events_claim_should_fail()
    {
        var jwt = CreateJwtWithTypAndClaims("JWT", new Dictionary<string, object>
        {
            { JwtClaimTypes.Subject, "valid" },
            { JwtClaimTypes.Events, "{\"http://schemas.openid.net/event/backchannel-logout\":{}}" }
        });
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.LogoutTokenShapeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_typ_jwt_and_plain_events_claim_should_succeed()
    {
        var jwt = CreateJwtWithTypAndClaims("JWT", new Dictionary<string, object>
        {
            { JwtClaimTypes.Subject, "valid" },
            { "events", "calendar" }
        });
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_typ_jwt_and_client_id_and_scope_claims_should_fail()
    {
        var jwt = CreateJwtWithTypAndClaims("JWT", new Dictionary<string, object>
        {
            { JwtClaimTypes.Subject, "valid" },
            { JwtClaimTypes.ClientId, "roclient" },
            { JwtClaimTypes.Scope, "api1" }
        });
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.AccessTokenShapeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_no_typ_and_client_id_and_scope_claims_should_fail()
    {
        var claims = new[]
        {
            new Claim(JwtClaimTypes.Subject, "valid"),
            new Claim(JwtClaimTypes.ClientId, "roclient"),
            new Claim(JwtClaimTypes.Scope, "api1"),
        };
        var jwt = CreateJwtWithoutTypHeader(claims);
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBe(IdentityTokenHintChecks.AccessTokenShapeReason);
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_typ_jwt_and_only_client_id_claim_should_succeed()
    {
        var jwt = CreateJwtWithTypAndClaims("JWT", new Dictionary<string, object>
        {
            { JwtClaimTypes.Subject, "valid" },
            { JwtClaimTypes.ClientId, "roclient" }
        });
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_typ_jwt_and_only_scope_claim_should_succeed()
    {
        var jwt = CreateJwtWithTypAndClaims("JWT", new Dictionary<string, object>
        {
            { JwtClaimTypes.Subject, "valid" },
            { JwtClaimTypes.Scope, "api1" }
        });
        var options = TestIdentityServerOptions.Create();

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void hint_with_custom_typ_and_client_id_and_scope_claims_should_succeed_when_configured()
    {
        var jwt = CreateJwtWithTypAndClaims("id_token+jwt", new Dictionary<string, object>
        {
            { JwtClaimTypes.Subject, "valid" },
            { JwtClaimTypes.ClientId, "roclient" },
            { JwtClaimTypes.Scope, "api1" }
        });
        var options = TestIdentityServerOptions.Create();
        options.IdentityTokenJwtTypes.Add("id_token+jwt");

        var failure = IdentityTokenHintChecks.Validate(jwt, CreateResult(ReadClaims(jwt)), options, NullLogger.Instance);

        failure.ShouldBeNull();
    }
}
