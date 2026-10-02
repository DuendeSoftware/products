// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using UnitTests.Common;

namespace UnitTests.Endpoints.Results;

public class EndSessionResultTests
{
    private EndSessionHttpWriter _subject;

    private EndSessionValidationResult _result = new EndSessionValidationResult();
    private IdentityServerOptions _options = new IdentityServerOptions();
    private MockMessageStore<LogoutMessage> _mockLogoutMessageStore = new MockMessageStore<LogoutMessage>();

    private DefaultServerUrls _urls;

    private DefaultHttpContext _context = new DefaultHttpContext();

    public EndSessionResultTests()
    {
        _urls = new DefaultServerUrls(new HttpContextAccessor { HttpContext = _context });

        _urls.Origin = "https://server";

        _options.UserInteraction.LogoutUrl = "~/logout";
        _options.UserInteraction.LogoutIdParameter = "logoutId";

        _subject = new EndSessionHttpWriter(_options, new FakeTimeProvider(), _urls, _mockLogoutMessageStore, new MockUiLocaleService());
    }

    [Fact]
    public async Task validated_signout_should_pass_logout_message()
    {
        _result.IsError = false;
        _result.ValidatedRequest = new ValidatedEndSessionRequest
        {
            Client = new Client
            {
                ClientId = "client"
            },
            PostLogOutUri = "http://client/post-logout-callback"
        };

        await _subject.WriteHttpResponse(new EndSessionResult(_result), _context);

        _mockLogoutMessageStore.Messages.Count.ShouldBe(1);
        var location = _context.Response.Headers.Location.Single();
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);

        location.ShouldStartWith("https://server/logout");
        query["logoutId"].First().ShouldBe(_mockLogoutMessageStore.Messages.First().Key);
    }

    [Fact]
    public async Task unvalidated_signout_should_not_pass_logout_message()
    {
        _result.IsError = false;

        await _subject.WriteHttpResponse(new EndSessionResult(_result), _context);

        _mockLogoutMessageStore.Messages.Count.ShouldBe(0);
        var location = _context.Response.Headers.Location.Single();
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);

        location.ShouldStartWith("https://server/logout");
        query.Count.ShouldBe(0);
    }

    [Fact]
    public async Task error_result_should_not_pass_logout_message()
    {
        _result.IsError = true;
        _result.ValidatedRequest = new ValidatedEndSessionRequest
        {
            Client = new Client
            {
                ClientId = "client"
            },
            PostLogOutUri = "http://client/post-logout-callback"
        };

        await _subject.WriteHttpResponse(new EndSessionResult(_result), _context);

        _mockLogoutMessageStore.Messages.Count.ShouldBe(0);
        var location = _context.Response.Headers.Location.Single();
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);

        location.ShouldStartWith("https://server/logout");
        query.Count.ShouldBe(0);
    }

    [Fact]
    public async Task validated_signout_with_saml_sessions_should_persist_logout_message_with_saml_logout_correlation_id()
    {
        _result.IsError = false;
        _result.ValidatedRequest = new ValidatedEndSessionRequest
        {
            Client = new Client
            {
                ClientId = "client"
            },
            PostLogOutUri = "http://client/post-logout-callback",
            SamlSessions = [
                new SamlSpSessionData
                {
                    EntityId = "https://sp.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session1"
                }
            ]
        };

        await _subject.WriteHttpResponse(new EndSessionResult(_result), _context);

        _mockLogoutMessageStore.Messages.Count.ShouldBe(1);
        var storedMessage = _mockLogoutMessageStore.Messages.Values.Single();
        storedMessage.Data.SamlLogoutCorrelationId.ShouldNotBeNullOrEmpty();
        storedMessage.Data.SamlLogoutCorrelationId!.Length.ShouldBe(32);

        // The value that flows into the redirect's logoutId is the store's protected handle, not the
        // SAML logout correlation ID.
        var location = _context.Response.Headers.Location.Single();
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        query["logoutId"].First().ShouldNotBe(storedMessage.Data.SamlLogoutCorrelationId);
        query["logoutId"].First().ShouldBe(_mockLogoutMessageStore.Messages.First().Key);
    }

    [Fact]
    public async Task validated_signout_without_saml_sessions_should_persist_logout_message_with_no_saml_logout_correlation_id()
    {
        _result.IsError = false;
        _result.ValidatedRequest = new ValidatedEndSessionRequest
        {
            Client = new Client
            {
                ClientId = "client"
            },
            PostLogOutUri = "http://client/post-logout-callback"
        };

        await _subject.WriteHttpResponse(new EndSessionResult(_result), _context);

        _mockLogoutMessageStore.Messages.Count.ShouldBe(1);
        var storedMessage = _mockLogoutMessageStore.Messages.Values.Single();
        storedMessage.Data.SamlLogoutCorrelationId.ShouldBeNull();
    }
}
