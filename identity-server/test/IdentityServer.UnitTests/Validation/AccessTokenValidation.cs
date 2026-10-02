// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation;

public class AccessTokenValidation
{
    private const string Category = "Access token validation";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private IClientStore _clients = Factory.CreateClientStore();
    private IdentityServerOptions _options = new IdentityServerOptions();
    private FakeTimeProvider _timeProvider = new FakeTimeProvider();

    static AccessTokenValidation() => JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

    private DateTime now;
    public DateTime UtcNow
    {
        get
        {
            if (now > DateTime.MinValue)
            {
                return now;
            }

            return DateTime.UtcNow;
        }
    }

    public AccessTokenValidation() => _timeProvider.SetUtcNow(UtcNow);

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Reference_Token()
    {
        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");

        var handle = await store.StoreReferenceTokenAsync(token, _ct);

        var result = await validator.ValidateAccessTokenAsync(handle, null, _ct);

        var claimTypes = result.Claims.Select(c => c.Type).ToList();
        claimTypes.ShouldContain("iss");
        claimTypes.ShouldContain("aud");
        claimTypes.ShouldContain("iat");
        claimTypes.ShouldContain("nbf");
        claimTypes.ShouldContain("exp");
        claimTypes.ShouldContain("client_id");
        claimTypes.ShouldContain("sub");
        claimTypes.ShouldContain("scope");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Reference_Token_with_required_Scope()
    {
        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");

        var handle = await store.StoreReferenceTokenAsync(token, _ct);

        var result = await validator.ValidateAccessTokenAsync(handle, "read", _ct);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Reference_Token_with_missing_Scope()
    {
        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");

        var handle = await store.StoreReferenceTokenAsync(token, _ct);

        var result = await validator.ValidateAccessTokenAsync(handle, "missing", _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InsufficientScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Reference_Token()
    {
        var validator = Factory.CreateTokenValidator();

        var result = await validator.ValidateAccessTokenAsync("unknown", null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Reference_Token_Too_Long()
    {
        var validator = Factory.CreateTokenValidator();
        var options = new IdentityServerOptions();

        var longToken = "x".Repeat(options.InputLengthRestrictions.TokenHandle + 1);
        var result = await validator.ValidateAccessTokenAsync(longToken, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
        // Asserts the token was rejected *for its length*, not merely because an unknown
        // handle misses in the reference token store (that path leaves ErrorDescription null).
        result.ErrorDescription.ShouldBe("Token too long");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Token_Longer_Than_Both_Limits_Is_Not_Scanned_Past_The_Length_Bound()
    {
        // Regression test for the DoS described in #3343: an over-long token must not be
        // scanned end-to-end while looking for the '.' that distinguishes a JWT from a
        // reference token.
        //
        // The scan cannot be observed directly, so this asserts on a side effect of it being
        // bounded. The token below places its only '.' *past* the scan window:
        //
        //   bounded scan   -> never sees the '.' -> classified as a reference token
        //   unbounded scan -> finds the '.'      -> classified as a JWT
        //
        // Both classifications are rejected for length and return an identical Error and
        // ErrorDescription, so the log message is the only thing that tells them apart.
        var options = TestIdentityServerOptions.Create();
        var logger = new CollectingLogger<TokenValidator>();
        var validator = Factory.CreateTokenValidator(options: options, logger: logger);

        var maxLength = Math.Max(options.InputLengthRestrictions.Jwt, options.InputLengthRestrictions.TokenHandle);
        var token = "x".Repeat(maxLength + 10) + ".";

        var result = await validator.ValidateAccessTokenAsync(token, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);

        // If this fails with "JWT too long", the '.' scan is reading past the length bound.
        logger.Messages.ShouldContain("token handle too long");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Token_Exceeding_Both_Limits_Without_Dot_Is_Rejected()
    {
        var validator = Factory.CreateTokenValidator();
        var options = new IdentityServerOptions();

        // A token without '.' that exceeds both Jwt and TokenHandle limits
        var maxLength = Math.Max(options.InputLengthRestrictions.Jwt, options.InputLengthRestrictions.TokenHandle);
        var longToken = "x".Repeat(maxLength + 1);
        var result = await validator.ValidateAccessTokenAsync(longToken, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
        result.ErrorDescription.ShouldBe("Token too long");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Token_Exceeding_Both_Limits_With_Dot_Is_Rejected()
    {
        var validator = Factory.CreateTokenValidator();
        var options = new IdentityServerOptions();

        // A token containing '.' that exceeds both Jwt and TokenHandle limits. Which of the
        // two limits it is measured against does not matter here, since it exceeds both.
        var maxLength = Math.Max(options.InputLengthRestrictions.Jwt, options.InputLengthRestrictions.TokenHandle);
        var longToken = "x".Repeat(maxLength) + ".y";
        var result = await validator.ValidateAccessTokenAsync(longToken, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
        result.ErrorDescription.ShouldBe("Token too long");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Expired_Reference_Token()
    {
        now = DateTime.UtcNow;

        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store, timeProvider: _timeProvider);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 2, "read", "write");
        token.CreationTime = now;

        var handle = await store.StoreReferenceTokenAsync(token, _ct);

        now = now.AddSeconds(3);
        _timeProvider.SetUtcNow(now);

        var result = await validator.ValidateAccessTokenAsync(handle, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.ExpiredToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Malformed_JWT_Token()
    {
        var validator = Factory.CreateTokenValidator();

        var result = await validator.ValidateAccessTokenAsync("unk.nown", null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_JWT_Token()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var jwt = await signer.CreateTokenAsync(TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write"), _ct);

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task JWT_without_client_id_claim_should_be_rejected()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "openid");
        token.Claims.Remove(token.Claims.Single(claim => claim.Type == "client_id"));
        var jwt = await signer.CreateTokenAsync(token, _ct);

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt, "openid", _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Theory]
    [InlineData("JWT")]
    [InlineData("")]
    [Trait("Category", Category)]
    public async Task ID_token_should_be_rejected_as_access_token_when_typ_check_is_relaxed(string accessTokenJwtType)
    {
        var options = TestIdentityServerOptions.Create();
        options.AccessTokenJwtType = accessTokenJwtType;
        var signer = Factory.CreateDefaultTokenCreator(options);
        var jwt = await signer.CreateTokenAsync(TokenFactory.CreateIdentityToken("roclient", "valid"), _ct);

        var validator = Factory.CreateTokenValidator(options: options);
        var result = await validator.ValidateAccessTokenAsync(jwt, "openid", _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Theory]
    [InlineData("openid")]
    [InlineData(null)]
    [Trait("Category", Category)]
    public async Task JWT_with_sub_minted_without_client_id_should_be_rejected(string expectedScope)
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var token = new Token(OidcConstants.TokenTypes.AccessToken)
        {
            CreationTime = DateTime.UtcNow,
            Issuer = "https://idsvr.com",
            Lifetime = 600,
            Claims = [new Claim(JwtClaimTypes.Subject, "valid"), new Claim(JwtClaimTypes.Scope, "openid")]
        };
        var jwt = await signer.CreateTokenAsync(token, _ct);

        var validator = Factory.CreateTokenValidator();
        var result = await validator.ValidateAccessTokenAsync(jwt, expectedScope, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task JWT_without_sub_or_client_id_should_remain_valid()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var token = new Token(OidcConstants.TokenTypes.AccessToken)
        {
            CreationTime = DateTime.UtcNow,
            Issuer = "https://idsvr.com",
            Lifetime = 600,
            Claims = [new Claim(JwtClaimTypes.Scope, "api1")]
        };
        var jwt = await signer.CreateTokenAsync(token, _ct);

        var validator = Factory.CreateTokenValidator();
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeFalse();
        result.Client.ShouldBeNull();
        result.Claims.ShouldContain(claim => claim.Type == JwtClaimTypes.Scope && claim.Value == "api1");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", Category)]
    public async Task JWT_Token_with_scopes_have_expected_claims(bool flag)
    {
        var options = TestIdentityServerOptions.Create();
        options.EmitScopesAsSpaceDelimitedStringInJwt = flag;

        var signer = Factory.CreateDefaultTokenCreator(options);
        var jwt = await signer.CreateTokenAsync(TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write"), _ct);

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeFalse();
        result.Jwt.ShouldNotBeNullOrEmpty();
        result.Client.ClientId.ShouldBe("roclient");

        result.Claims.Count().ShouldBe(9);
        var scopes = result.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToArray();
        scopes.Length.ShouldBe(2);
        scopes[0].ShouldBe("read");
        scopes[1].ShouldBe("write");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task JWT_Token_invalid_Issuer()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");
        token.Issuer = "invalid";
        var jwt = await signer.CreateTokenAsync(token, _ct);

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task JWT_Token_Too_Long()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var jwt = await signer.CreateTokenAsync(TokenFactory.CreateAccessTokenLong(new Client { ClientId = "roclient" }, "valid", 600, 1000, "read", "write"), _ct);

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
        // Distinguishes length rejection from a signature/format failure, both of which
        // would otherwise surface as a bare invalid_token.
        result.ErrorDescription.ShouldBe("Token too long");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task JWT_respects_timeProvider_skew_setting_to_allow_token_with_off_time()
    {
        var futureClock = new FakeTimeProvider();
        var definitelyNotNow = DateTime.UtcNow.AddSeconds(9);
        futureClock.SetUtcNow(definitelyNotNow);
        var signer = Factory.CreateDefaultTokenCreator(timeProvider: futureClock);
        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");
        var jwt = await signer.CreateTokenAsync(token, _ct);

        var options = TestIdentityServerOptions.Create();
        options.JwtValidationClockSkew = TimeSpan.FromSeconds(10);
        var validator = Factory.CreateTokenValidator(options: options);
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Jwt_when_token_time_outside_of_configured_timeProvider_skew_token_is_considered_invalid()
    {
        var futureClock = new FakeTimeProvider();
        var definitelyNotNow = DateTime.UtcNow.AddSeconds(10);
        futureClock.SetUtcNow(definitelyNotNow);
        var signer = Factory.CreateDefaultTokenCreator(timeProvider: futureClock);
        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");
        var jwt = await signer.CreateTokenAsync(token, _ct);

        var options = TestIdentityServerOptions.Create();
        options.JwtValidationClockSkew = TimeSpan.FromSeconds(5);
        var validator = Factory.CreateTokenValidator(options: options);
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unrelated_supported_signing_algorithm_options_are_not_enforced()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");
        var jwt = await signer.CreateTokenAsync(token, _ct);

        var options = TestIdentityServerOptions.Create();
        options.SupportedRequestObjectSigningAlgorithms = ["Test"];
        options.SupportedClientAssertionSigningAlgorithms = ["Test"];
        var validator = Factory.CreateTokenValidator(options: options);
        var result = await validator.ValidateAccessTokenAsync(jwt, null, _ct);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_AccessToken_but_Client_not_active()
    {
        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "unknown" }, "valid", 600, "read", "write");

        var handle = await store.StoreReferenceTokenAsync(token, _ct);

        var result = await validator.ValidateAccessTokenAsync(handle, null, _ct);

        result.IsError.ShouldBeTrue();
    }
}
