// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using FluentAssertions;
using System.Collections.Specialized;
using System.Threading.Tasks;
using UnitTests.Common;
using Xunit;

namespace UnitTests.Services.Default;

public class ParRedirectUriValidatorTests
{
    [Fact]
    public async Task PushedRedirectUriCanBeUsedAsync()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var redirectUri = "https://pushed.example.com";
        var pushedParameters = new NameValueCollection
        {
            { "redirect_uri", redirectUri }
        };

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.AuthorizeWithPushedParameters,
            RequestParameters = pushedParameters,
            RequestedUri = redirectUri,
            Client = new Client
            {
                RequireClientSecret = true,
            }
        });

        result.Should().Be(true);
    }

    [Fact]
    public async Task AnythingIsPermittedAtParEndpoint()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var redirectUri = "https://pushed.example.com";
        var pushedParameters = new NameValueCollection
        {
            { "redirect_uri", redirectUri }
        };

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.PushedAuthorization,
            RequestParameters = pushedParameters,
            RequestedUri = redirectUri,
            Client = new Client
            {
                RequireClientSecret = true,
            }
        });

        result.Should().Be(true);
    }

    [Fact]
    public async Task ConfigurationControlsPermissiveness()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = false;
        var subject = new StrictRedirectUriValidator(options);
        var pushedRedirectUri = "https://pushed.example.com";
        var pushedParameters = new NameValueCollection
        {
            { "redirect_uri", pushedRedirectUri }
        };

        var notThePushedRedirectUri = "https://dangerous.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.AuthorizeWithPushedParameters,
            RequestParameters = pushedParameters,
            RequestedUri = notThePushedRedirectUri,
            Client = new Client()
        });

        result.Should().Be(false);
    }

    [Fact]
    public async Task UsingARegisteredPushedUriInsteadOfThePushedRedirectUriShouldSucceed()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var pushedRedirectUri = "https://pushed.example.com";
        var pushedParameters = new NameValueCollection
        {
            { "redirect_uri", pushedRedirectUri }
        };

        var registeredRedirectUri = "https://registered.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.AuthorizeWithPushedParameters,
            RequestParameters = pushedParameters,
            RequestedUri = registeredRedirectUri,
            Client = new Client
            {
                RedirectUris = { "https://registered.example.com" }
            }
        });

        registeredRedirectUri.Should().NotBe(pushedRedirectUri);
        result.Should().Be(true);
    }

    [Fact]
    public async Task AuthorizeEndpointWithoutPushedParametersIsStillStrict()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var requestedRedirectUri = "https://requested.example.com";
        var authorizeParameters = new NameValueCollection
        {
            { "redirect_uri", requestedRedirectUri }
        };

        var registeredRedirectUri = "https://registered.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.Authorize,
            RequestParameters = authorizeParameters,
            RequestedUri = requestedRedirectUri,
            Client = new Client
            {
                RedirectUris = { "https://registered.example.com" }
            }
        });

        registeredRedirectUri.Should().NotBe(requestedRedirectUri);
        result.Should().Be(false);
    }
}
