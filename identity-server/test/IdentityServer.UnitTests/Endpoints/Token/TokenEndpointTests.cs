// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace IdentityServer.Endpoints.Token;

public class TokenEndpointTests
{
    private HttpContext _context;

    private readonly IdentityServerOptions _identityServerOptions = new IdentityServerOptions();

    private readonly StubClientSecretValidator _stubClientSecretValidator = new StubClientSecretValidator();

    private readonly StubTokenRequestValidator _stubTokenRequestValidator = new StubTokenRequestValidator();

    private readonly StubTokenResponseGenerator _stubTokenResponseGenerator = new StubTokenResponseGenerator();

    private readonly TestEventService _fakeEventService = new TestEventService();

    private readonly ILogger<TokenEndpoint> _fakeLogger = TestLogger.Create<TokenEndpoint>();

    private TokenEndpoint _subject;

    public TokenEndpointTests() => Init();

    [Fact]
    public async Task ProcessAsync_should_not_raise_event_on_use_dpop_nonce_token_validation_failure()
    {
        _context.Request.Method = "POST";
        _context.Request.Headers.ContentType = "application/x-www-form-urlencoded";

        var client = new Client
        {
            ClientId = "client",
            ClientName = "Test Client",
        };

        _stubClientSecretValidator.Result = new ClientSecretValidationResult
        {
            IsError = false,
            Client = client
        };

        var validatedTokenRequest = new ValidatedTokenRequest
        {
            Client = client,
            GrantType = OidcConstants.GrantTypes.AuthorizationCode
        };
        _stubTokenRequestValidator.Result = new TokenRequestValidationResult(validatedTokenRequest, OidcConstants.TokenErrors.UseDPoPNonce);

        await _subject.ProcessAsync(_context);

        _fakeEventService.AssertEventWasNotRaised<TokenIssuedFailureEvent>();
    }

    private void Init()
    {
        _context = new MockHttpContextAccessor().HttpContext;

        _subject = new TokenEndpoint(
            _identityServerOptions,
            _stubClientSecretValidator,
            _stubTokenRequestValidator,
            _stubTokenResponseGenerator,
            _fakeEventService,
            _fakeLogger);
    }
}
