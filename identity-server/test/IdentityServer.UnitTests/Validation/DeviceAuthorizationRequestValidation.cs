// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation;

public class DeviceAuthorizationRequestValidation
{
    private const string Category = "Device authorization request validation";

    private readonly NameValueCollection testParameters = new NameValueCollection { { "scope", "resource" } };
    private readonly Client testClient = new Client
    {
        ClientId = "device_flow",
        AllowedGrantTypes = GrantTypes.DeviceFlow,
        AllowedScopes = { "openid", "profile", "resource" },
        AllowOfflineAccess = true
    };

    [Fact]
    [Trait("Category", Category)]
    public async Task Null_Parameter()
    {
        var validator = Factory.CreateDeviceAuthorizationRequestValidator();

        Func<Task> act = () => validator.ValidateAsync(null, null, default);

        await act.ShouldThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Protocol_Client()
    {
        testClient.ProtocolType = IdentityServerConstants.ProtocolTypes.WsFederation;

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(testParameters, new ClientSecretValidationResult { Client = testClient }, default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Grant_Type()
    {
        testClient.AllowedGrantTypes = GrantTypes.Implicit;

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(testParameters, new ClientSecretValidationResult { Client = testClient }, default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unauthorized_Scope()
    {
        var parameters = new NameValueCollection { { "scope", "resource2" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient }, default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Scope()
    {
        var parameters = new NameValueCollection { { "scope", Guid.NewGuid().ToString() } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient }, default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_Request()
    {
        var parameters = new NameValueCollection { { "scope", "openid" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient }, default);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.IsOpenIdRequest.ShouldBeTrue();
        result.ValidatedRequest.RequestedScopes.ShouldContain("openid");

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.ShouldContain(x => x.Name == "openid");
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.ShouldBeEmpty();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.ShouldBeFalse();

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Any().ShouldBeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Any().ShouldBeFalse();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Resource_Request()
    {
        var parameters = new NameValueCollection { { "scope", "resource" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient }, default);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.IsOpenIdRequest.ShouldBeFalse();
        result.ValidatedRequest.RequestedScopes.ShouldContain("resource");

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.ShouldBeEmpty();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.ShouldContain(x => x.Name == "api");
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.ShouldContain(x => x.Name == "resource");
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.ShouldBeFalse();

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Any().ShouldBeFalse();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Any().ShouldBeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.Any().ShouldBeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Mixed_Request()
    {
        var parameters = new NameValueCollection { { "scope", "openid resource offline_access" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient }, default);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.IsOpenIdRequest.ShouldBeTrue();
        result.ValidatedRequest.RequestedScopes.ShouldContain("openid");
        result.ValidatedRequest.RequestedScopes.ShouldContain("resource");
        result.ValidatedRequest.RequestedScopes.ShouldContain("offline_access");

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.ShouldContain(x => x.Name == "openid");
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.ShouldContain(x => x.Name == "api");
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.ShouldContain(x => x.Name == "resource");
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.ShouldBeTrue();

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Any().ShouldBeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Any().ShouldBeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.Any().ShouldBeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.ShouldBeTrue();
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_Scopes_Expect_Client_Scopes()
    {
        var validator = Factory.CreateDeviceAuthorizationRequestValidator();

        var result = await validator.ValidateAsync(
            new NameValueCollection(),
            new ClientSecretValidationResult { Client = testClient },
            default);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.RequestedScopes.ShouldContain(testClient.AllowedScopes);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_Scopes_And_Client_Scopes_Empty()
    {
        testClient.AllowedScopes.Clear();
        var validator = Factory.CreateDeviceAuthorizationRequestValidator();

        var result = await validator.ValidateAsync(
            new NameValueCollection(),
            new ClientSecretValidationResult { Client = testClient },
            default);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
    }
}
