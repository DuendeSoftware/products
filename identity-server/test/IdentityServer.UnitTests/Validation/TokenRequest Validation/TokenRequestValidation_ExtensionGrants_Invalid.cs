// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Stores;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.TokenRequest_Validation;

public class TokenRequestValidation_ExtensionGrants_Invalid
{
    private const string Category = "TokenRequest Validation - Extension Grants - Invalid";

    private IClientStore _clients = Factory.CreateClientStore();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Extension_Grant_Type_For_Client_Credentials_Client()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, "customGrant" },
            { OidcConstants.TokenRequest.Scope, "resource" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnsupportedGrantType);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Restricted_Extension_Grant_Type()
    {
        var client = await _clients.FindEnabledClientByIdAsync("customgrantclient", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, "unknown_grant_type" },
            { OidcConstants.TokenRequest.Scope, "resource" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnsupportedGrantType);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Customer_Error_and_Description_Extension_Grant_Type()
    {
        var client = await _clients.FindEnabledClientByIdAsync("customgrantclient", _ct);

        var validator = Factory.CreateTokenRequestValidator(extensionGrantValidators: new[] { new TestGrantValidator(isInvalid: true, errorDescription: "custom error description") });

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, "custom_grant" },
            { OidcConstants.TokenRequest.Scope, "resource" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
        result.ErrorDescription.ShouldBe("custom error description");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task inactive_user_should_fail()
    {
        var client = await _clients.FindEnabledClientByIdAsync("customgrantclient", _ct);

        var validator = Factory.CreateTokenRequestValidator(
            profile: new TestProfileService(shouldBeActive: false));

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, "custom_grant" },
            { OidcConstants.TokenRequest.Scope, "resource" }
        };

        var result = await validator.ValidateRequestAsync(
            parameters,
            client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
    }
}
