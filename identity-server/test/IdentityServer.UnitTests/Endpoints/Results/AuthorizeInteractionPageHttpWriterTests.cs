// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Endpoints.Results;

public class AuthorizeInteractionPageHttpWriterTests
{
    private readonly AuthorizeInteractionPageHttpWriter _subject;
    private readonly IdentityServerOptions _options = new IdentityServerOptions();
    private readonly DefaultServerUrls _urls;
    private readonly DefaultHttpContext _context = new DefaultHttpContext();
    private readonly MockUiLocaleService _localesService = new MockUiLocaleService();

    public AuthorizeInteractionPageHttpWriterTests()
    {
        _options.UserInteraction.LoginUrl = "~/account/login";
        _options.UserInteraction.LoginReturnUrlParameter = "returnUrl";
        _options.UserInteraction.ConsentUrl = "~/consent";
        _options.UserInteraction.ConsentReturnUrlParameter = "returnUrl";
        _options.UserInteraction.CustomRedirectReturnUrlParameter = "returnUrl";

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IUiLocalesService>(_localesService);
        _context.RequestServices = serviceCollection.BuildServiceProvider();

        _urls = new DefaultServerUrls(new HttpContextAccessor { HttpContext = _context });

        _context.Request.Scheme = "https";
        _context.Request.Host = new HostString("server");
        _context.Response.Body = new MemoryStream();

        _subject = new AuthorizeInteractionPageHttpWriter(_options, _urls, _localesService);
    }

    [Fact]
    public async Task should_redirect_to_login_page_with_return_url()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            Raw = new NameValueCollection
            {
                { "client_id", "client" },
                { "response_type", "code" }
            }
        };

        var result = new LoginPageResult(request, _options);

        await _subject.WriteHttpResponse(result, _context);

        _context.Response.StatusCode.ShouldBe(StatusCodes.Status303SeeOther);
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("https://server/account/login");
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        query[_options.UserInteraction.LoginReturnUrlParameter].First().ShouldContain("connect/authorize/callback");
    }

    [Fact]
    public async Task should_redirect_to_consent_page_with_return_url()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            Raw = new NameValueCollection
            {
                { "client_id", "client" },
                { "response_type", "code" }
            }
        };

        var result = new ConsentPageResult(request, _options);

        await _subject.WriteHttpResponse(result, _context);

        _context.Response.StatusCode.ShouldBe(StatusCodes.Status303SeeOther);
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("https://server/consent");
    }

    [Fact]
    public async Task PAR_flow_should_include_request_uri_and_client_id()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            PushedAuthorizationReferenceValue = "par-ref-123",
            Raw = new NameValueCollection
            {
                { "client_id", "client" }
            }
        };

        var result = new LoginPageResult(request, _options);

        await _subject.WriteHttpResponse(result, _context);

        var location = _context.Response.Headers.Location.First();
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        var returnUrl = query[_options.UserInteraction.LoginReturnUrlParameter].First();
        var returnQuery = QueryHelpers.ParseQuery(new Uri("https://dummy" + returnUrl).Query);
        returnQuery[OidcConstants.AuthorizeRequest.RequestUri].First().ShouldContain("par-ref-123");
        returnQuery[OidcConstants.AuthorizeRequest.ClientId].First().ShouldBe("client");
    }

    [Fact]
    public async Task PAR_flow_should_include_processed_prompt_and_max_age()
    {
        var processedPrompt = "suppressed_prompt";
        var processedMaxAge = "suppressed_max_age";

        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            PushedAuthorizationReferenceValue = "par-ref-123",
            Raw = new NameValueCollection
            {
                { "client_id", "client" },
                { processedPrompt, "login" },
                { processedMaxAge, "300" }
            }
        };

        var result = new LoginPageResult(request, _options);

        await _subject.WriteHttpResponse(result, _context);

        var location = _context.Response.Headers.Location.First();
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        var returnUrl = query[_options.UserInteraction.LoginReturnUrlParameter].First();
        var returnQuery = QueryHelpers.ParseQuery(new Uri("https://dummy" + returnUrl).Query);
        returnQuery[processedPrompt].First().ShouldBe("login");
        returnQuery[processedMaxAge].First().ShouldBe("300");
    }

    [Fact]
    public async Task non_PAR_flow_should_include_full_query_string()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            Raw = new NameValueCollection
            {
                { "client_id", "client" },
                { "response_type", "code" },
                { "scope", "openid" }
            }
        };

        var result = new LoginPageResult(request, _options);

        await _subject.WriteHttpResponse(result, _context);

        var location = _context.Response.Headers.Location.First();
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        var returnUrl = query[_options.UserInteraction.LoginReturnUrlParameter].First();
        returnUrl.ShouldContain("client_id=client");
        returnUrl.ShouldContain("response_type=code");
        returnUrl.ShouldContain("scope=openid");
    }

    [Fact]
    public async Task external_redirect_should_use_absolute_return_url()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            Raw = new NameValueCollection
            {
                { "client_id", "client" },
                { "response_type", "code" }
            }
        };

        var result = new CustomRedirectResult(request, "https://external.example.com/login", _options);

        await _subject.WriteHttpResponse(result, _context);

        _context.Response.StatusCode.ShouldBe(StatusCodes.Status303SeeOther);
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("https://external.example.com/login");
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        var returnUrl = query[_options.UserInteraction.CustomRedirectReturnUrlParameter].First();
        returnUrl.ShouldStartWith("https://server");
    }

    // Subclass that demonstrates the customer scenario: set a custom cookie,
    // suppress the returnUrl query parameter, and redirect to an external page.
    private class CustomCookieWriter : AuthorizeInteractionPageHttpWriter
    {
        public const string CookieName = "login-context";
        public const string CookieValue = "custom-value";
        public const string ExternalLoginUrl = "https://external-idp.example.com/login";

        public CustomCookieWriter(IdentityServerOptions options, IServerUrls urls, IUiLocalesService localesService)
            : base(options, urls, localesService) { }

        protected override Task<string> BuildRedirectUrlAsync(
            AuthorizeInteractionPageResult result, string returnUrl, HttpContext context) =>
            // Redirect to an external page without appending returnUrl
            Task.FromResult(ExternalLoginUrl);

        protected override Task WriteResponseAsync(HttpContext context, string redirectUrl)
        {
            context.Response.Cookies.Append(CookieName, CookieValue);
            return base.WriteResponseAsync(context, redirectUrl);
        }
    }

    [Fact]
    public async Task subclass_can_set_custom_cookie_during_redirect()
    {
        var subject = new CustomCookieWriter(_options, _urls, _localesService);

        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            Raw = new NameValueCollection
            {
                { "client_id", "client" },
                { "response_type", "code" }
            }
        };

        var result = new LoginPageResult(request, _options);

        await subject.WriteHttpResponse(result, _context);

        _context.Response.Headers["Set-Cookie"].First().ShouldContain(CustomCookieWriter.CookieName);
        _context.Response.Headers["Set-Cookie"].First().ShouldContain(CustomCookieWriter.CookieValue);
    }

    [Fact]
    public async Task subclass_can_redirect_to_external_page_without_return_url()
    {
        var subject = new CustomCookieWriter(_options, _urls, _localesService);

        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            Raw = new NameValueCollection
            {
                { "client_id", "client" },
                { "response_type", "code" }
            }
        };

        var result = new LoginPageResult(request, _options);

        await subject.WriteHttpResponse(result, _context);

        _context.Response.StatusCode.ShouldBe(StatusCodes.Status303SeeOther);
        var location = _context.Response.Headers.Location.First();
        location.ShouldBe(CustomCookieWriter.ExternalLoginUrl);
    }
}
