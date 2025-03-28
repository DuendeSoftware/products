// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Endpoints.Results;

public class AuthorizeResultTests
{
    private AuthorizeHttpWriter _subject;

    private AuthorizeResponse _response = new AuthorizeResponse();
    private IdentityServerOptions _options = new IdentityServerOptions();
    private MockUserSession _mockUserSession = new MockUserSession();
    private MockMessageStore<Duende.IdentityServer.Models.ErrorMessage> _mockErrorMessageStore = new MockMessageStore<Duende.IdentityServer.Models.ErrorMessage>();

    private DefaultServerUrls _urls;
    private DefaultHttpContext _context = new DefaultHttpContext();

    public AuthorizeResultTests()
    {
        _urls = new DefaultServerUrls(new HttpContextAccessor { HttpContext = _context });

        _context.Request.Scheme = "https";
        _context.Request.Host = new HostString("server");
        _context.Response.Body = new MemoryStream();

        _options.UserInteraction.ErrorUrl = "~/error";
        _options.UserInteraction.ErrorIdParameter = "errorId";

        _subject = new AuthorizeHttpWriter(_options, _mockUserSession, new TestPushedAuthorizationService(), _mockErrorMessageStore, _urls, new StubClock());
    }

    [Fact]
    public async Task error_should_redirect_to_error_page_and_passs_info()
    {
        _response.Error = "some_error";

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _mockErrorMessageStore.Messages.Count.ShouldBe(1);
        _context.Response.StatusCode.ShouldBe(302);
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("https://server/error");
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        query["errorId"].First().ShouldBe(_mockErrorMessageStore.Messages.First().Key);
    }

    [Theory]
    [InlineData(OidcConstants.AuthorizeErrors.AccountSelectionRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.LoginRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.ConsentRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.InteractionRequired)]
    public async Task prompt_none_errors_should_return_to_client(string error)
    {
        _response.Error = error;
        _response.Request = new ValidatedAuthorizeRequest
        {
            ResponseMode = OidcConstants.ResponseModes.Query,
            RedirectUri = "http://client/callback",
            PromptModes = new[] { "none" }
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _mockUserSession.Clients.Count.ShouldBe(0);
        _context.Response.StatusCode.ShouldBe(302);
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("http://client/callback");
    }

    [Theory]
    [InlineData(OidcConstants.AuthorizeErrors.AccountSelectionRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.LoginRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.ConsentRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.InteractionRequired)]
    public async Task prompt_none_errors_for_anonymous_users_should_include_session_state(string error)
    {
        _response.Error = error;
        _response.Request = new ValidatedAuthorizeRequest
        {
            ResponseMode = OidcConstants.ResponseModes.Query,
            RedirectUri = "http://client/callback",
            PromptModes = new[] { "none" },
        };
        _response.SessionState = "some_session_state";

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _mockUserSession.Clients.Count.ShouldBe(0);
        _context.Response.StatusCode.ShouldBe(302);
        var location = _context.Response.Headers.Location.First();
        location.ShouldContain("session_state=some_session_state");
    }

    [Fact]
    public async Task access_denied_should_return_to_client()
    {
        const string errorDescription = "some error description";

        _response.Error = OidcConstants.AuthorizeErrors.AccessDenied;
        _response.ErrorDescription = errorDescription;
        _response.Request = new ValidatedAuthorizeRequest
        {
            ResponseMode = OidcConstants.ResponseModes.Query,
            RedirectUri = "http://client/callback"
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _mockUserSession.Clients.Count.ShouldBe(0);
        _context.Response.StatusCode.ShouldBe(302);
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("http://client/callback");

        var queryString = new Uri(location).Query;
        var queryParams = QueryHelpers.ParseQuery(queryString);

        queryParams["error"].ToString().ShouldBe(OidcConstants.AuthorizeErrors.AccessDenied);
        queryParams["error_description"].ToString().ShouldBe(errorDescription);
    }

    [Fact]
    public async Task success_should_add_client_to_client_list()
    {
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            ResponseMode = OidcConstants.ResponseModes.Query,
            RedirectUri = "http://client/callback"
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _mockUserSession.Clients.ShouldContain("client");
    }

    [Fact]
    public async Task query_mode_should_pass_results_in_query()
    {
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            ResponseMode = OidcConstants.ResponseModes.Query,
            RedirectUri = "http://client/callback",
            State = "state"
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _context.Response.StatusCode.ShouldBe(302);
        _context.Response.Headers.CacheControl.First().ShouldContain("no-store");
        _context.Response.Headers.CacheControl.First().ShouldContain("no-cache");
        _context.Response.Headers.CacheControl.First().ShouldContain("max-age=0");
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("http://client/callback");
        location.ShouldContain("?state=state");
    }

    [Fact]
    public async Task fragment_mode_should_pass_results_in_fragment()
    {
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            RedirectUri = "http://client/callback",
            State = "state"
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _context.Response.StatusCode.ShouldBe(302);
        _context.Response.Headers.CacheControl.First().ShouldContain("no-store");
        _context.Response.Headers.CacheControl.First().ShouldContain("no-cache");
        _context.Response.Headers.CacheControl.First().ShouldContain("max-age=0");
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("http://client/callback");
        location.ShouldContain("#state=state");
    }

    [Fact]
    public async Task form_post_mode_should_pass_results_in_body()
    {
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            ResponseMode = OidcConstants.ResponseModes.FormPost,
            RedirectUri = "http://client/callback",
            State = "state"
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _context.Response.StatusCode.ShouldBe(200);
        _context.Response.ContentType.ShouldStartWith("text/html");
        _context.Response.Headers.CacheControl.First().ShouldContain("no-store");
        _context.Response.Headers.CacheControl.First().ShouldContain("no-cache");
        _context.Response.Headers.CacheControl.First().ShouldContain("max-age=0");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain("default-src 'none';");
        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.AuthorizeScript}'");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain("default-src 'none';");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain($"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.AuthorizeScript}'");
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(_context.Response.Body);
        var html = await rdr.ReadToEndAsync();
        html.ShouldContain("<base target='_self'/>");
        html.ShouldContain("<form method='post' action='http://client/callback'>");
        html.ShouldContain("<input type='hidden' name='state' value='state' />");
    }

    [Fact]
    public async Task form_post_mode_should_add_unsafe_inline_for_csp_level_1()
    {
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            ResponseMode = OidcConstants.ResponseModes.FormPost,
            RedirectUri = "http://client/callback",
            State = "state"
        };

        _options.Csp.Level = CspLevel.One;

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"script-src 'unsafe-inline' '{IdentityServerConstants.ContentSecurityPolicyHashes.AuthorizeScript}'");
        _context.Response.Headers["X-Content-Security-Policy"].First().ShouldContain($"script-src 'unsafe-inline' '{IdentityServerConstants.ContentSecurityPolicyHashes.AuthorizeScript}'");
    }

    [Fact]
    public async Task form_post_mode_should_not_add_deprecated_header_when_it_is_disabled()
    {
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            ResponseMode = OidcConstants.ResponseModes.FormPost,
            RedirectUri = "http://client/callback",
            State = "state"
        };

        _options.Csp.AddDeprecatedHeader = false;

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _context.Response.Headers.ContentSecurityPolicy.First().ShouldContain($"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.AuthorizeScript}'");
        _context.Response.Headers["X-Content-Security-Policy"].ShouldBeEmpty();
    }

    [InlineData(OidcConstants.AuthorizeErrors.AccessDenied)]
    [InlineData(OidcConstants.AuthorizeErrors.AccountSelectionRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.LoginRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.ConsentRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.InteractionRequired)]
    [InlineData(OidcConstants.AuthorizeErrors.TemporarilyUnavailable)]
    [InlineData(OidcConstants.AuthorizeErrors.UnmetAuthenticationRequirements)]
    [Theory]
    public async Task error_resulting_in_redirect_should_attach_fragment_to_location_header(string error)
    {
        _response.Error = error;
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            GrantType = OidcConstants.GrantTypes.Implicit,
            ResponseMode = OidcConstants.ResponseModes.Query,
            RedirectUri = "http://client/callback",
            ResponseType = OidcConstants.ResponseTypes.Token,
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _context.Response.StatusCode.ShouldBe(302);
        var location = _context.Response.Headers.Location.First();
        location.ShouldStartWith("http://client/callback");
        location.ShouldContain("#_");
    }

    [InlineData(OidcConstants.AuthorizeErrors.InvalidRequest)]
    [InlineData(OidcConstants.AuthorizeErrors.UnauthorizedClient)]
    [InlineData(OidcConstants.AuthorizeErrors.UnsupportedResponseType)]
    [InlineData(OidcConstants.AuthorizeErrors.InvalidScope)]
    [InlineData(OidcConstants.AuthorizeErrors.ServerError)]
    [InlineData(OidcConstants.AuthorizeErrors.InvalidRequestUri)]
    [InlineData(OidcConstants.AuthorizeErrors.InvalidRequestObject)]
    [InlineData(OidcConstants.AuthorizeErrors.RequestNotSupported)]
    [InlineData(OidcConstants.AuthorizeErrors.RequestUriNotSupported)]
    [InlineData(OidcConstants.AuthorizeErrors.RegistrationNotSupported)]
    [InlineData(OidcConstants.AuthorizeErrors.InvalidTarget)]
    [Theory]
    public async Task error_resulting_in_error_page_should_attach_fragment_to_error_model_redirect_uri(string error)
    {
        _response.Error = error;
        _response.Request = new ValidatedAuthorizeRequest
        {
            ClientId = "client",
            GrantType = OidcConstants.GrantTypes.Implicit,
            ResponseMode = OidcConstants.ResponseModes.Query,
            RedirectUri = "http://client/callback",
            ResponseType = OidcConstants.ResponseTypes.Token,
        };

        await _subject.WriteHttpResponse(new AuthorizeResult(_response), _context);

        _context.Response.StatusCode.ShouldBe(302);
        var location = _context.Response.Headers.Location.First();
        var queryString = new Uri(location).Query;
        var queryParams = QueryHelpers.ParseQuery(queryString);
        var errorId = queryParams.First(kvp => kvp.Key == _options.UserInteraction.ErrorIdParameter).Value;
        var errorMessage = await _mockErrorMessageStore.ReadAsync(errorId);
        errorMessage.Data.RedirectUri.ShouldContain("#_");
    }
}
