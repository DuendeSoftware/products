// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.TokenRequest_Validation;

public class TokenRequestValidation_DeviceCode_Invalid
{
    private const string Category = "TokenRequest Validation - DeviceCode - Invalid";

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly IClientStore _clients = Factory.CreateClientStore();

    private readonly DeviceCode deviceCode = new DeviceCode
    {
        ClientId = "device_flow",
        IsAuthorized = true,
        Subject = new IdentityServerUser("bob").CreatePrincipal(),
        IsOpenId = true,
        Lifetime = 300,
        CreationTime = DateTime.UtcNow,
        AuthorizedScopes = new[] { "openid", "profile", "resource" }
    };

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_DeviceCode()
    {
        var client = await _clients.FindClientByIdAsync("device_flow", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection
        {
            {OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.DeviceCode}
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DeviceCode_Too_Long()
    {
        var client = await _clients.FindClientByIdAsync("device_flow", _ct);

        var longCode = "x".Repeat(new IdentityServerOptions().InputLengthRestrictions.AuthorizationCode + 1);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection
        {
            {OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.DeviceCode},
            {"device_code", longCode}
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Grant_For_Client()
    {
        var client = await _clients.FindClientByIdAsync("codeclient", _ct);

        var validator = Factory.CreateTokenRequestValidator();

        var parameters = new NameValueCollection
        {
            {OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.DeviceCode},
            {"device_code", Guid.NewGuid().ToString()}
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DeviceCodeValidator_Failure()
    {
        var client = await _clients.FindClientByIdAsync("device_flow", _ct);

        var validator = Factory.CreateTokenRequestValidator(deviceCodeValidator: new TestDeviceCodeValidator(true));

        var parameters = new NameValueCollection
        {
            {OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.DeviceCode},
            {"device_code", Guid.NewGuid().ToString()}
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);
        result.IsError.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_resource_indicator()
    {
        var client = await _clients.FindClientByIdAsync("device_flow", _ct);

        var validator = Factory.CreateTokenRequestValidator(deviceCodeValidator: new TestDeviceCodeValidator(true));

        var parameters = new NameValueCollection
        {
            {OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.DeviceCode},
            {"device_code", Guid.NewGuid().ToString()},
            { OidcConstants.TokenRequest.Resource, "api" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task resource_indicator_should_not_be_allowed()
    {
        var client = await _clients.FindClientByIdAsync("device_flow", _ct);

        var validator = Factory.CreateTokenRequestValidator(deviceCodeValidator: new TestDeviceCodeValidator(true));

        var parameters = new NameValueCollection
        {
            {OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.DeviceCode},
            {"device_code", Guid.NewGuid().ToString()},
            { OidcConstants.TokenRequest.Resource, "urn:api1" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("invalid_target");
    }
}
