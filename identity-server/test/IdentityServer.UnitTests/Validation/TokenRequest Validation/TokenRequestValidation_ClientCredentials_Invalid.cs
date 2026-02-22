// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Stores;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.TokenRequest_Validation;

public class TokenRequestValidation_ClientCredentials_Invalid
{
    private const string Category = "TokenRequest Validation - ClientCredentials - Invalid";

    private IClientStore _clients = Factory.CreateClientStore();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_GrantType_For_Client()
    {
        var client = await _clients.FindEnabledClientByIdAsync("roclient", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Request_should_succeed_even_with_allowed_identity_scopes_because_they_are_filtered_out()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Select(x => x.Name).ShouldBe(["api", "urn:api1", "urn:api2", "urn:api3"]);
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["resource", "resource2", "scope1"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Scope()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "unknown");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Scope_Multiple()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource unknown");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Restricted_Scope()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client_restricted", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource2");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Restricted_Scope_Multiple()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client_restricted", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource resource2");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Identity_scope_is_not_allowed_for_client_credentials_when_specified_explicitly()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials },
            { OidcConstants.TokenRequest.Scope, "openid" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Resource_and_Refresh_Token()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "resource offline_access");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidScope);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_resource_indicator()
    {
        var client = await _clients.FindEnabledClientByIdAsync("client", _ct);
        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials);
        parameters.Add(OidcConstants.TokenRequest.Scope, "scope1");

        {
            parameters[OidcConstants.TokenRequest.Resource] = "urn:api1" + new string('x', 512);
            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidTarget);
        }
        {
            parameters[OidcConstants.TokenRequest.Resource] = "api";

            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());
            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
        {
            parameters[OidcConstants.TokenRequest.Resource] = "urn:api1";
            parameters.Add(OidcConstants.TokenRequest.Resource, "urn:api2");

            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());
            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
    }
}
