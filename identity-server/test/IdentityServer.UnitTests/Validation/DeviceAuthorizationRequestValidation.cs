// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using FluentAssertions;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using UnitTests.Validation.Setup;
using Xunit;

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

        Func<Task> act = () => validator.ValidateAsync(null, null);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Protocol_Client()
    {
        testClient.ProtocolType = IdentityServerConstants.ProtocolTypes.WsFederation;

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(testParameters, new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeTrue();
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Grant_Type()
    {
        testClient.AllowedGrantTypes = GrantTypes.Implicit;

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(testParameters, new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeTrue();
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unauthorized_Scope()
    {
        var parameters = new NameValueCollection { { "scope", "resource2" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeTrue();
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_Scope()
    {
        var parameters = new NameValueCollection { { "scope", Guid.NewGuid().ToString() } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeTrue();
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_Request()
    {
        var parameters = new NameValueCollection { { "scope", "openid" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeFalse();
        result.ValidatedRequest.IsOpenIdRequest.Should().BeTrue();
        result.ValidatedRequest.RequestedScopes.Should().Contain("openid");

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Should().Contain(x => x.Name == "openid");
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Should().BeEmpty();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.Should().BeFalse();

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Any().Should().BeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Any().Should().BeFalse();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Resource_Request()
    {
        var parameters = new NameValueCollection { { "scope", "resource" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeFalse();
        result.ValidatedRequest.IsOpenIdRequest.Should().BeFalse();
        result.ValidatedRequest.RequestedScopes.Should().Contain("resource");

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Should().BeEmpty();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Should().Contain(x => x.Name == "api");
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.Should().Contain(x => x.Name == "resource");
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.Should().BeFalse();

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Any().Should().BeFalse();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Any().Should().BeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.Any().Should().BeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Mixed_Request()
    {
        var parameters = new NameValueCollection { { "scope", "openid resource offline_access" } };

        var validator = Factory.CreateDeviceAuthorizationRequestValidator();
        var result = await validator.ValidateAsync(parameters, new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeFalse();
        result.ValidatedRequest.IsOpenIdRequest.Should().BeTrue();
        result.ValidatedRequest.RequestedScopes.Should().Contain("openid");
        result.ValidatedRequest.RequestedScopes.Should().Contain("resource");
        result.ValidatedRequest.RequestedScopes.Should().Contain("offline_access");

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Should().Contain(x => x.Name == "openid");
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Should().Contain(x => x.Name == "api");
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.Should().Contain(x => x.Name == "resource");
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.Should().BeTrue();

        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources.Any().Should().BeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.Any().Should().BeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.ApiScopes.Any().Should().BeTrue();
        result.ValidatedRequest.ValidatedResources.Resources.OfflineAccess.Should().BeTrue();
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_Scopes_Expect_Client_Scopes()
    {
        var validator = Factory.CreateDeviceAuthorizationRequestValidator();

        var result = await validator.ValidateAsync(
            new NameValueCollection(),
            new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeFalse();
        result.ValidatedRequest.RequestedScopes.Should().Contain(testClient.AllowedScopes);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_Scopes_And_Client_Scopes_Empty()
    {
        testClient.AllowedScopes.Clear();
        var validator = Factory.CreateDeviceAuthorizationRequestValidator();

        var result = await validator.ValidateAsync(
            new NameValueCollection(),
            new ClientSecretValidationResult { Client = testClient });

        result.IsError.Should().BeTrue();
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidScope);
    }
}
