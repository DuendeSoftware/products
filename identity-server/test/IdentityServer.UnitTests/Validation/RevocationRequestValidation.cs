// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using UnitTests.Common;

namespace UnitTests.Validation;

public class RevocationRequestValidation
{
    private const string Category = "Revocation Request Validation Tests";

    private ITokenRevocationRequestValidator _validator;
    private Client _client;

    public RevocationRequestValidation()
    {
        _validator = new TokenRevocationRequestValidator(TestLogger.Create<TokenRevocationRequestValidator>());
        _client = new Client
        {
            ClientName = "Code Client",
            Enabled = true,
            ClientId = "codeclient",
            ClientSecrets = new List<Secret>
            {
                new Secret("secret".Sha256())
            },

            AllowedGrantTypes = GrantTypes.Code,

            RequireConsent = false,

            RedirectUris = new List<string>
            {
                "https://server/cb"
            },

            AuthorizationCodeLifetime = 60
        };
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Empty_Parameters()
    {
        var parameters = new NameValueCollection();

        var result = await _validator.ValidateRequestAsync(parameters, _client, default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_Token_Valid_Hint()
    {
        var parameters = new NameValueCollection
        {
            { "token_type_hint", "access_token" }
        };

        var result = await _validator.ValidateRequestAsync(parameters, _client, default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Token_And_AccessTokenHint()
    {
        var parameters = new NameValueCollection
        {
            { "token", "foo" },
            { "token_type_hint", "access_token" }
        };

        var result = await _validator.ValidateRequestAsync(parameters, _client, default);

        result.IsError.ShouldBeFalse();
        result.Token.ShouldBe("foo");
        result.TokenTypeHint.ShouldBe("access_token");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Token_and_RefreshTokenHint()
    {
        var parameters = new NameValueCollection
        {
            { "token", "foo" },
            { "token_type_hint", "refresh_token" }
        };

        var result = await _validator.ValidateRequestAsync(parameters, _client, default);

        result.IsError.ShouldBeFalse();
        result.Token.ShouldBe("foo");
        result.TokenTypeHint.ShouldBe("refresh_token");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Token_And_Missing_Hint()
    {
        var parameters = new NameValueCollection
        {
            { "token", "foo" }
        };

        var result = await _validator.ValidateRequestAsync(parameters, _client, default);

        result.IsError.ShouldBeFalse();
        result.Token.ShouldBe("foo");
        result.TokenTypeHint.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Token_And_Invalid_Hint()
    {
        var parameters = new NameValueCollection
        {
            { "token", "foo" },
            { "token_type_hint", "invalid" }
        };

        var result = await _validator.ValidateRequestAsync(parameters, _client, default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(Constants.RevocationErrors.UnsupportedTokenType);
    }
}
