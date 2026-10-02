// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Specialized;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class ParRedirectUriValidatorTests
{
    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task implicit_only_client_should_not_allow_unregistered_pushed_redirect_uri(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var redirectUri = "https://pushed.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestParameters = new NameValueCollection { { "redirect_uri", redirectUri } },
            RequestedUri = redirectUri,
            Client = new Client
            {
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireClientSecret = true,
            }
        }, default);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task implicit_only_client_should_allow_registered_pushed_redirect_uri(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var redirectUri = "https://registered.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestParameters = new NameValueCollection { { "redirect_uri", redirectUri } },
            RequestedUri = redirectUri,
            Client = new Client
            {
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireClientSecret = true,
                RedirectUris = { redirectUri }
            }
        }, default);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task implicit_and_client_credentials_client_should_allow_unregistered_pushed_redirect_uri(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var redirectUri = "https://pushed.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestParameters = new NameValueCollection { { "redirect_uri", redirectUri } },
            RequestedUri = redirectUri,
            Client = new Client
            {
                AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
                RequireClientSecret = true,
            }
        }, default);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task client_not_requiring_secret_should_not_allow_unregistered_pushed_redirect_uri(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var redirectUri = "https://pushed.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestParameters = new NameValueCollection { { "redirect_uri", redirectUri } },
            RequestedUri = redirectUri,
            Client = new Client
            {
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireClientSecret = false,
            }
        }, default);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task implicit_only_client_should_fall_back_to_virtual_redirect_validation()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new PermissiveRedirectUriValidator(options);
        var redirectUri = "https://pushed.example.com";

        var result = await subject.IsRedirectUriValidAsync(new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.PushedAuthorization,
            RequestParameters = new NameValueCollection { { "redirect_uri", redirectUri } },
            RequestedUri = redirectUri,
            Client = new Client
            {
                AllowedGrantTypes = GrantTypes.Implicit,
                RequireClientSecret = true,
            }
        }, default);

        subject.WasInvoked.ShouldBeTrue();
        result.ShouldBeTrue();
    }

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
        }, default);

        result.ShouldBe(true);
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
        }, default);

        result.ShouldBe(true);
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
        }, default);

        result.ShouldBe(false);
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
        }, default);

        registeredRedirectUri.ShouldNotBe(pushedRedirectUri);
        result.ShouldBe(true);
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
        }, default);

        registeredRedirectUri.ShouldNotBe(requestedRedirectUri);
        result.ShouldBe(false);
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization, "https://allowed.example/cb", true)]
    [InlineData(AuthorizeRequestType.PushedAuthorization, "HTTPS://allowed.example/cb", true)]
    [InlineData(AuthorizeRequestType.PushedAuthorization, "http://not-allowed.example/cb", false)]
    [InlineData(AuthorizeRequestType.PushedAuthorization, "http://localhost/cb", false)]
    [InlineData(AuthorizeRequestType.PushedAuthorization, "http://127.0.0.1:5000/cb", false)]
    [InlineData(AuthorizeRequestType.PushedAuthorization, "myapp://callback", false)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters, "https://allowed.example/cb", true)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters, "HTTPS://allowed.example/cb", true)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters, "http://not-allowed.example/cb", false)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters, "http://localhost/cb", false)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters, "http://127.0.0.1:5000/cb", false)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters, "myapp://callback", false)]
    public async Task unregistered_redirect_uri_should_require_https(
        AuthorizeRequestType requestType, string requestedUri, bool expectedResult)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestedUri = requestedUri,
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("\tjavascript:alert(1)")]
    [InlineData("\rjavascript:alert(1)")]
    [InlineData("\njavascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("DATA:text/html,x")]
    [InlineData(" data:text/html,x")]
    [InlineData("\tdata:text/html,x")]
    [InlineData("\rdata:text/html,x")]
    [InlineData("\ndata:text/html,x")]
    public async Task javascript_and_data_redirect_uris_should_be_rejected(string requestedUri)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.PushedAuthorization,
            RequestedUri = requestedUri,
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task blocked_prefix_matching_should_be_case_insensitive()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        options.Validation.InvalidRedirectUriPrefixes.Add("HTTPS://Blocked.Example");
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.PushedAuthorization,
            RequestedUri = "https://blocked.example/x",
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task leading_whitespace_should_refuse_relaxation_without_mutating_requested_uri()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var requestedUri = "\thttps://allowed.example/cb";
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.PushedAuthorization,
            RequestedUri = requestedUri,
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeFalse();
        context.RequestedUri.ShouldBe(requestedUri);
    }

    [Theory]
    [InlineData("https://blocked.example/x", false)]
    [InlineData("https://ok.example/x", true)]
    public async Task arbitrary_literal_prefix_should_block_only_matching_values(string requestedUri, bool expectedResult)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        options.Validation.InvalidRedirectUriPrefixes.Add("https://blocked.example");
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.PushedAuthorization,
            RequestedUri = requestedUri,
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task malformed_absolute_uri_should_refuse_relaxation()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.PushedAuthorization,
            RequestedUri = "\0javascript:alert(1)",
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task disabled_opt_in_should_remain_strict(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestedUri = "https://unregistered.example/cb",
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task public_client_should_not_use_unregistered_redirect_uri(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestedUri = "https://unregistered.example/cb",
            Client = new Client { RequireClientSecret = false }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ordinary_authorize_request_should_remain_strict()
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = AuthorizeRequestType.Authorize,
            RequestedUri = "https://unregistered.example/cb",
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task registered_non_https_scheme_should_use_strict_fallback(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new StrictRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestedUri = "myapp://registered",
            Client = new Client
            {
                RequireClientSecret = true,
                RedirectUris = { "myapp://registered" }
            }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(AuthorizeRequestType.PushedAuthorization)]
    [InlineData(AuthorizeRequestType.AuthorizeWithPushedParameters)]
    public async Task virtual_override_should_remain_authoritative_on_fallthrough(AuthorizeRequestType requestType)
    {
        var options = TestIdentityServerOptions.Create();
        options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
        var subject = new PermissiveRedirectUriValidator(options);
        var context = new RedirectUriValidationContext
        {
            AuthorizeRequestType = requestType,
            RequestedUri = "myapp://callback",
            Client = new Client { RequireClientSecret = true }
        };

        var result = await subject.IsRedirectUriValidAsync(context, default);

        result.ShouldBeTrue();
        subject.WasInvoked.ShouldBeTrue();
    }

    private sealed class PermissiveRedirectUriValidator(IdentityServerOptions options) : StrictRedirectUriValidator(options)
    {
        public bool WasInvoked { get; private set; }

        public override Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client)
        {
            WasInvoked = true;
            return Task.FromResult(true);
        }
    }
}
