// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Shouldly;
using Duende.IdentityModel;
using UnitTests.Common;
using UnitTests.Validation.Setup;
using Xunit;

namespace UnitTests.Validation;

public class AccessTokenValidation
{
    private const string Category = "Access token validation";

    private IClientStore _clients = Factory.CreateClientStore();
    private IdentityServerOptions _options = new IdentityServerOptions();
    private StubClock _clock = new StubClock();

    static AccessTokenValidation()
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
    }

    private DateTime now;
    public DateTime UtcNow
    {
        get
        {
            if (now > DateTime.MinValue) return now;
            return DateTime.UtcNow;
        }
    }

    public AccessTokenValidation()
    {
        _clock.UtcNowFunc = () => UtcNow;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Reference_Token()
    {
        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");

        var handle = await store.StoreReferenceTokenAsync(token);

        var result = await validator.ValidateAccessTokenAsync(handle);

        result.IsError.ShouldBeFalse();
        result.Claims.Count().ShouldBe(9);
        result.Claims.First(c => c.Type == JwtClaimTypes.ClientId).Value.ShouldBe("roclient");

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

        var handle = await store.StoreReferenceTokenAsync(token);

        var result = await validator.ValidateAccessTokenAsync(handle, "read");

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Reference_Token_with_missing_Scope()
    {
        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write");

        var handle = await store.StoreReferenceTokenAsync(token);

        var result = await validator.ValidateAccessTokenAsync(handle, "missing");

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InsufficientScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Reference_Token()
    {
        var validator = Factory.CreateTokenValidator();

        var result = await validator.ValidateAccessTokenAsync("unknown");

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
        var result = await validator.ValidateAccessTokenAsync(longToken);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Expired_Reference_Token()
    {
        now = DateTime.UtcNow;

        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store, clock:_clock);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 2, "read", "write");
        token.CreationTime = now;

        var handle = await store.StoreReferenceTokenAsync(token);

        now = now.AddSeconds(3);

        var result = await validator.ValidateAccessTokenAsync(handle);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.ExpiredToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Malformed_JWT_Token()
    {
        var validator = Factory.CreateTokenValidator();

        var result = await validator.ValidateAccessTokenAsync("unk.nown");

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_JWT_Token()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var jwt = await signer.CreateTokenAsync(TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write"));

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt);

        result.IsError.ShouldBeFalse();
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
        var jwt = await signer.CreateTokenAsync(TokenFactory.CreateAccessToken(new Client { ClientId = "roclient" }, "valid", 600, "read", "write"));

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt);

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
        var jwt = await signer.CreateTokenAsync(token);

        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task JWT_Token_Too_Long()
    {
        var signer = Factory.CreateDefaultTokenCreator();
        var jwt = await signer.CreateTokenAsync(TokenFactory.CreateAccessTokenLong(new Client { ClientId = "roclient" }, "valid", 600, 1000, "read", "write"));
            
        var validator = Factory.CreateTokenValidator(null);
        var result = await validator.ValidateAccessTokenAsync(jwt);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_AccessToken_but_Client_not_active()
    {
        var store = Factory.CreateReferenceTokenStore();
        var validator = Factory.CreateTokenValidator(store);

        var token = TokenFactory.CreateAccessToken(new Client { ClientId = "unknown" }, "valid", 600, "read", "write");

        var handle = await store.StoreReferenceTokenAsync(token);

        var result = await validator.ValidateAccessTokenAsync(handle);

        result.IsError.ShouldBeTrue();
    }
}