// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.TokenRequest_Validation;

public class TokenRequestValidation_Invalid
{
    private const string Category = "TokenRequest Validation - General - Invalid";

    private readonly IClientStore _clients = Factory.CreateClientStore();

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_refresh_token_request_should_fail()
    {
        var subjectClaim = new Claim(JwtClaimTypes.Subject, "foo");

        var refreshToken = new RefreshToken
        {
            ClientId = "roclient",
            Subject = new IdentityServerUser(subjectClaim.Value).CreatePrincipal(),
            AuthorizedScopes = null, // Passing a null value to AuthorizedScopes should be invalid
            Lifetime = 600,
            CreationTime = DateTime.UtcNow
        };

        refreshToken.SetAccessToken(new Token("access_token")
        {
            Claims = [subjectClaim],
            ClientId = "roclient"
        });

        var grants = Factory.CreateRefreshTokenStore();
        var handle = await grants.StoreRefreshTokenAsync(refreshToken);

        var client = await _clients.FindEnabledClientByIdAsync("roclient");

        var validator = Factory.CreateTokenRequestValidator(refreshTokenStore: grants);

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, "refresh_token" },
            { OidcConstants.TokenRequest.RefreshToken, handle }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
    }
}
