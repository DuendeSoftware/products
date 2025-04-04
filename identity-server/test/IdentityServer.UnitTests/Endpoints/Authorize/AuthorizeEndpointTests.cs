// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Endpoints.Authorize;

public class AuthorizeEndpointTests
{
    private const string Category = "Authorize Endpoint";

    private HttpContext _context;

    private TestEventService _fakeEventService = new TestEventService();

    private ILogger<AuthorizeEndpoint> _fakeLogger = TestLogger.Create<AuthorizeEndpoint>();

    private IdentityServerOptions _options = new IdentityServerOptions();

    private MockUserSession _mockUserSession = new MockUserSession();

    private NameValueCollection _params = new NameValueCollection();

    private StubAuthorizeRequestValidator _stubAuthorizeRequestValidator = new StubAuthorizeRequestValidator();

    private StubAuthorizeResponseGenerator _stubAuthorizeResponseGenerator = new StubAuthorizeResponseGenerator();

    private StubAuthorizeInteractionResponseGenerator _stubInteractionGenerator = new StubAuthorizeInteractionResponseGenerator();

    private MockConsentMessageStore _mockUserConsentResponseMessageStore = new MockConsentMessageStore();

    private AuthorizeEndpoint _subject;

    private ClaimsPrincipal _user = new IdentityServerUser("bob").CreatePrincipal();

    private ValidatedAuthorizeRequest _validatedAuthorizeRequest;

    public AuthorizeEndpointTests() => Init();

    [Fact]
    [Trait("Category", Category)]
    public async Task ProcessAsync_authorize_path_should_return_authorization_result()
    {
        _context.Request.Method = "GET";
        _context.Request.Path = new PathString("/connect/authorize");
        _mockUserSession.User = _user;

        var result = await _subject.ProcessAsync(_context);

        result.ShouldBeOfType<AuthorizeResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ProcessAsync_post_without_form_content_type_should_return_415()
    {
        _context.Request.Method = "POST";

        var result = await _subject.ProcessAsync(_context);

        var statusCode = result as StatusCodeResult;
        statusCode.ShouldNotBeNull();
        statusCode.StatusCode.ShouldBe(415);
    }

    internal void Init()
    {
        _context = new MockHttpContextAccessor().HttpContext;

        _validatedAuthorizeRequest = new ValidatedAuthorizeRequest()
        {
            RedirectUri = "http://client/callback",
            State = "123",
            ResponseMode = "fragment",
            ClientId = "client",
            Client = new Client
            {
                ClientId = "client",
                ClientName = "Test Client"
            },
            Raw = _params,
            Subject = _user
        };
        _stubAuthorizeResponseGenerator.Response.Request = _validatedAuthorizeRequest;

        _stubAuthorizeRequestValidator.Result = new AuthorizeRequestValidationResult(_validatedAuthorizeRequest);

        _subject = new AuthorizeEndpoint(
            _fakeEventService,
            _fakeLogger,
            _options,
            _stubAuthorizeRequestValidator,
            _stubInteractionGenerator,
            _stubAuthorizeResponseGenerator,
            _mockUserSession,
            _mockUserConsentResponseMessageStore);
    }
}
