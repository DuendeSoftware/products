// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.TokenRequest_Validation;

public class TokenRequestValidation_ResourceOwner_Invalid
{
    private const string Category = "TokenRequest Validation - ResourceOwner - Invalid";

    private IClientStore _clients = Factory.CreateClientStore();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_GrantType_For_Client()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Scope()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "unknown");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Scope_Multiple()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource unknown");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Restricted_Scope()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient_restricted", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource2");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Restricted_Scope_Multiple()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient_restricted", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource resource2");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task No_ResourceOwnerCredentials()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_ResourceOwner_UserName()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResourceOwner_Credentials()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "notbob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
        result.ErrorDescription.ShouldBe("invalid_username_or_password");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_ResourceOwner_password_for_user_with_password_should_fail()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob_with_password");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Password_GrantType_Not_Supported()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator(resourceOwnerValidator: new NotSupportedResourceOwnerPasswordValidator(TestLogger.Create<NotSupportedResourceOwnerPasswordValidator>()));

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnsupportedGrantType);
        result.ErrorDescription.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Inactive_ResourceOwner()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator(profile: new TestProfileService(shouldBeActive: false));

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Password_GrantType_With_Custom_ErrorDescription()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator(resourceOwnerValidator: new TestResourceOwnerPasswordValidator(TokenRequestErrors.InvalidGrant, "custom error description"));

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "notbob");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
        result.ErrorDescription.ShouldBe("custom error description");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task failed_resource_validation_should_fail()
    {
        var client = (await _clients.FindEnabledClientByIdAsync("roclient", _ct)).ToValidationResult();
        var mockResourceValidator = new MockResourceValidator();
        var validator = Factory.CreateTokenRequestValidator(resourceValidator: mockResourceValidator);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.Password);
        parameters.Add(OidcConstants.TokenRequest.Scope, "scope1");
        parameters.Add(OidcConstants.TokenRequest.UserName, "bob");
        parameters.Add(OidcConstants.TokenRequest.Password, "bob");
        parameters.Add("resource", "urn:api1");

        {
            mockResourceValidator.Result = new ResourceValidationResult
            {
                InvalidScopes = { "foo" }
            };
            var result = await validator.ValidateRequestAsync(parameters, client);

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_scope");
        }

        {
            mockResourceValidator.Result = new ResourceValidationResult
            {
                InvalidResourceIndicators = { "foo" }
            };
            var result = await validator.ValidateRequestAsync(parameters, client);

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
    }
}
