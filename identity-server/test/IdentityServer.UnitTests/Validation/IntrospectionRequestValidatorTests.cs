// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation;

public class IntrospectionRequestValidatorTests
{
    private const string Category = "Introspection request validation";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private IntrospectionRequestValidator _subject;
    private IReferenceTokenStore _referenceTokenStore;

    public IntrospectionRequestValidatorTests()
    {
        _referenceTokenStore = Factory.CreateReferenceTokenStore();
        var tokenValidator = Factory.CreateTokenValidator(_referenceTokenStore);
        var refreshTokenService = Factory.CreateRefreshTokenService();

        _subject = new IntrospectionRequestValidator(tokenValidator, refreshTokenService, TestLogger.Create<IntrospectionRequestValidator>());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_token_should_successfully_validate()
    {
        var token = new Token
        {
            CreationTime = DateTime.UtcNow,
            Issuer = "http://op",
            ClientId = "codeclient",
            Lifetime = 1000,
            Claims =
            {
                new System.Security.Claims.Claim("scope", "a"),
                new System.Security.Claims.Claim("scope", "b")
            }
        };
        var handle = await _referenceTokenStore.StoreReferenceTokenAsync(token, _ct);

        var param = new NameValueCollection()
        {
            { "token", handle}
        };

        var result = await _subject.ValidateAsync(
            new IntrospectionRequestValidationContext
            {
                Parameters = param,
                Api = new ApiResource("api")
            },
            _ct
        );

        result.IsError.ShouldBe(false);
        result.IsActive.ShouldBe(true);
        result.Claims.Count().ShouldBe(6);
        result.Token.ShouldBe(handle);

        var claimTypes = result.Claims.Select(c => c.Type).ToList();
        claimTypes.ShouldContain("iss");
        claimTypes.ShouldContain("scope");
        claimTypes.ShouldContain("iat");
        claimTypes.ShouldContain("nbf");
        claimTypes.ShouldContain("exp");

    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_token_should_error()
    {
        var param = new NameValueCollection();

        var result = await _subject.ValidateAsync(new IntrospectionRequestValidationContext
        {
            Parameters = param,
            Api = new ApiResource("api")
        }, _ct);

        result.IsError.ShouldBe(true);
        result.Error.ShouldBe("missing_token");
        result.IsActive.ShouldBe(false);
        result.Claims.ShouldBeNull();
        result.Token.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_token_should_return_inactive()
    {
        var param = new NameValueCollection()
        {
            { "token", "invalid" }
        };

        var result = await _subject.ValidateAsync(new IntrospectionRequestValidationContext
        {
            Parameters = param,
            Api = new ApiResource("api")
        }, _ct);

        result.IsError.ShouldBe(false);
        result.IsActive.ShouldBe(false);
        result.Claims.ShouldBeNull();
        result.Token.ShouldBe("invalid");
    }

    [Theory]
    [MemberData(nameof(DuplicateClaimTestCases))]
    [Trait("Category", Category)]
    public async Task protocol_claims_should_not_be_duplicated(
        string claimType,
        System.Security.Claims.Claim duplicateClaim,
        Func<Token, string> expectedValueSelector)
    {
        var token = new Token
        {
            CreationTime = DateTime.UtcNow,
            Issuer = "http://op",
            ClientId = "codeclient",
            Lifetime = 1000,
            Claims =
            {
                duplicateClaim
            }
        };

        var handle = await _referenceTokenStore.StoreReferenceTokenAsync(token, _ct);
        var param = new NameValueCollection
        {
            { "token", handle }
        };

        var result = await _subject.ValidateAsync(
            new IntrospectionRequestValidationContext
            {
                Parameters = param,
                Api = new ApiResource("api")
            },
            _ct
        );

        var claims = result.Claims.Where(c => c.Type == claimType).ToArray();
        claims.Length.ShouldBe(1);
        claims.ShouldContain(c => c.Value == expectedValueSelector(token));
    }

    public static IEnumerable<object[]> DuplicateClaimTestCases()
    {
        yield return new object[]
        {
            JwtClaimTypes.IssuedAt,
            new System.Security.Claims.Claim(JwtClaimTypes.IssuedAt, "1234"),
            (Func<Token, string>)(token => new DateTimeOffset(token.CreationTime).ToUnixTimeSeconds().ToString())
        };

        yield return new object[]
        {
            JwtClaimTypes.Issuer,
            new System.Security.Claims.Claim(JwtClaimTypes.Issuer, "https://bogus.example.com"),
            (Func<Token, string>)(token => token.Issuer)
        };

        yield return new object[]
        {
            JwtClaimTypes.NotBefore,
            new System.Security.Claims.Claim(JwtClaimTypes.NotBefore, "1234"),
            (Func<Token, string>)(token => new DateTimeOffset(token.CreationTime).ToUnixTimeSeconds().ToString())
        };

        yield return new object[]
        {
            JwtClaimTypes.Expiration,
            new System.Security.Claims.Claim(JwtClaimTypes.Expiration, "1234"),
            (Func<Token, string>)(token => new DateTimeOffset(token.CreationTime).AddSeconds(token.Lifetime).ToUnixTimeSeconds().ToString())
        };
    }
}
