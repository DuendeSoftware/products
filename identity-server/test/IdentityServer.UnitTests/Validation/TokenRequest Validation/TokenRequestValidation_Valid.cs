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

public class TokenRequestValidation_Valid
{
    private const string Category = "TokenRequest Validation - General - Valid";

    private readonly CT _ct = TestContext.Current.CancellationToken;
    private IClientStore _clients = Factory.CreateClientStore();
    private TestDeviceCodeValidator _mockDeviceCodeValidator = new TestDeviceCodeValidator();

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_ResourceOwner_password_for_user_with_no_password_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob_no_password");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.UserName.ShouldBe("bob_no_password");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_code_request_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_code_request_with_refresh_token_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid",
                "offline_access"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_client_credentials_request_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_client_credentials_request_with_default_scopes_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client_restricted", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);


        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_client_credentials_request_for_implicit_and_client_credentials_client_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("implicit_and_client_creds_client", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_client_credentials_request_restricted_client_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client_restricted", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_resource_owner_request_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_resource_wwner_request_with_refresh_token_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource offline_access");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_resource_owner_request_restricted_client_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient_restricted", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_extension_grant_request_should_succeed()
    {
        var client = await _clients.FindEnabledClientByIdAsync("customgrantclient", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, "custom_grant");
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_refresh_token_request_should_succeed()
    {
        var subjectClaim = new Claim(JwtClaimTypes.Subject, "foo");

        var refreshToken = new RefreshToken
        {
            ClientId = "roclient",
            Subject = new IdentityServerUser(subjectClaim.Value).CreatePrincipal(),
            AuthorizedScopes = Enumerable.Empty<string>(),
            Lifetime = 600,
            CreationTime = DateTime.UtcNow
        };
        refreshToken.SetAccessToken(new Token("access_token")
        {
            Claims = new List<Claim> { subjectClaim },
            ClientId = "roclient"
        });

        var grants = Factory.CreateRefreshTokenStore();
        var handle = await grants.StoreRefreshTokenAsync(refreshToken);

        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);

        var validator = Factory.CreateTokenRequestValidator(
            refreshTokenStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, "refresh_token");
        parameters.Add(OidcConstants.TokenRequest.RefreshToken, handle);

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_refresh_token_request_using_restricted_client_should_succeed()
    {
        var subjectClaim = new Claim(JwtClaimTypes.Subject, "foo");

        var refreshToken = new RefreshToken
        {
            ClientId = "roclient_restricted_refresh",
            Subject = new IdentityServerUser(subjectClaim.Value).CreatePrincipal(),
            AuthorizedScopes = Enumerable.Empty<string>(),
            Lifetime = 600,
            CreationTime = DateTime.UtcNow
        };

        var grants = Factory.CreateRefreshTokenStore();
        var handle = await grants.StoreRefreshTokenAsync(refreshToken);

        var client = await _clients.FindEnabledClientByIdAsync("roclient_restricted_refresh", _ct);

        var validator = Factory.CreateTokenRequestValidator(
            refreshTokenStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, "refresh_token");
        parameters.Add(OidcConstants.TokenRequest.RefreshToken, handle);

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_device_code_request_should_succeed()
    {
        var deviceCode = new DeviceCode
        {
            ClientId = "device_flow",
            IsAuthorized = true,
            Subject = new IdentityServerUser("bob").CreatePrincipal(),
            IsOpenId = true,
            Lifetime = 300,
            CreationTime = DateTime.UtcNow,
            AuthorizedScopes = new[] { "openid", "profile", "resource" }
        };

        var client = await _clients.FindClientByIdAsync("device_flow", _ct);

        var validator = Factory.CreateTokenRequestValidator(deviceCodeValidator: _mockDeviceCodeValidator);
        _mockDeviceCodeValidator.DeviceCodeResult = deviceCode;

        var parameters = new NameValueCollection
        {
            {OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.DeviceCode},
            {"device_code", Guid.NewGuid().ToString()}
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());
        result.IsError.ShouldBeFalse();
    }
}
