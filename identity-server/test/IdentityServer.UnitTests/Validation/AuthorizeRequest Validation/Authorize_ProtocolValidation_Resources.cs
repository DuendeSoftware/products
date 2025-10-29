// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Logging;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.AuthorizeRequest_Validation;

public class Authorize_ProtocolValidation_Resources
{
    private const string Category = "AuthorizeRequest Protocol Validation - Resources";

    private readonly AuthorizeRequestValidator _subject;

    private readonly IdentityServerOptions _options = new IdentityServerOptions();
    private readonly MockResourceValidator _mockResourceValidator = new MockResourceValidator();
    private readonly MockUserSession _mockUserSession = new MockUserSession();

    private readonly List<Client> _clients = new List<Client>()
    {
        new Client{
            ClientId = "client1",
            RequirePkce = false,
            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes = { "openid", "scope1" },
            RedirectUris = { "https://client1" },
        },
        new Client{
            ClientId = "client2",
            AllowedGrantTypes = GrantTypes.Implicit,
            AllowedScopes = { "scope1" },
            AllowAccessTokensViaBrowser = true,
            RedirectUris = { "https://client2" },
        },
    };

    public Authorize_ProtocolValidation_Resources() => _subject = new AuthorizeRequestValidator(
            _options,
            new TestIssuerNameService("https://sts"),
            new InMemoryClientStore(_clients),
            new DefaultCustomAuthorizeRequestValidator(),
            new StrictRedirectUriValidator(_options),
            _mockResourceValidator,
            _mockUserSession,
            Factory.CreateRequestObjectValidator(),
            new LicenseUsageTracker(new LicenseAccessor(() => null, NullLogger<LicenseAccessor>.Instance), new NullLoggerFactory()),
            new ClientLoadedTracker(),
            new ResourceLoadedTracker(),
            new SanitizedLogger<AuthorizeRequestValidator>(TestLogger.Create<AuthorizeRequestValidator>()));

    [Fact]
    [Trait("Category", Category)]
    public async Task no_resourceindicators_should_succeed()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client1");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client1");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var result = await _subject.ValidateAsync(parameters);

        result.IsError.ShouldBe(false);
        result.ValidatedRequest.RequestedResourceIndicators.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_uri_resourceindicator_should_fail()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client1");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client1");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add("resource", "not_uri");

        var result = await _subject.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_uri_resourceindicator_should_succeed()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client1");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client1");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add("resource", "http://resource1");

        var result = await _subject.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task implicit_request_with_resourceindicator_should_fail()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client2");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client2");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Token);
        parameters.Add("resource", "http://resource1");

        var result = await _subject.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task resourceindicator_too_long_should_fail()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client1");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client1");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add("resource", "http://resource1" + new string('x', 512));

        var result = await _subject.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task fragment_in_resourceindicator_should_fail()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client1");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client1");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add("resource", "http://resource1#fragment");

        var result = await _subject.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("invalid_target");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multiple_uri_resourceindicators_should_succeed()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client1");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client1");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add("resource", "http://resource1");
        parameters.Add("resource", "http://resource2");
        parameters.Add("resource", "urn:test1");

        var result = await _subject.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.RequestedResourceIndicators
            .ShouldBe(["urn:test1", "http://resource1", "http://resource2"], true);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task failed_resource_validation_should_fail()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client1");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid scope1");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://client1");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add("resource", "http://resource1");

        {
            _mockResourceValidator.Result = new ResourceValidationResult
            {
                InvalidScopes = { "foo" }
            };
            var result = await _subject.ValidateAsync(parameters);

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_scope");
        }

        {
            _mockResourceValidator.Result = new ResourceValidationResult
            {
                InvalidResourceIndicators = { "foo" }
            };
            var result = await _subject.ValidateAsync(parameters);

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
    }
}
