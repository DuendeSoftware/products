// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.RegularExpressions;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UnitTests.Common;
using SamlLogoutRequest = Duende.IdentityServer.Saml.Samlp.LogoutRequest;

namespace UnitTests.Endpoints.Results;

public class Saml2LogoutPageResultTests
{
    private static readonly Regex HexRegex = new("^[0-9A-Fa-f]{32}$");

    private readonly MockUserSession _userSession = new();
    private readonly MockMessageStore<LogoutMessage> _messageStore = new();
    private readonly DefaultServerUrls _serverUrls;
    private readonly DefaultHttpContext _context = new();
    private readonly IdentityServerOptions _options = new()
    {
        UserInteraction = { LogoutUrl = "~/logout", LogoutIdParameter = "logoutId" }
    };

    private readonly Saml2LogoutPageResultHttpWriter _subject;

    public Saml2LogoutPageResultTests()
    {
        _serverUrls = new DefaultServerUrls(new HttpContextAccessor { HttpContext = _context })
        {
            Origin = "https://server"
        };

        _subject = new Saml2LogoutPageResultHttpWriter(
            _userSession,
            _messageStore,
            _serverUrls,
            new FakeTimeProvider(),
            Options.Create(_options));
    }

    private static ValidatedLogoutRequest CreateRequest() => new()
    {
        LogoutRequest = new SamlLogoutRequest { Id = "_req-id" },
        Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect",
        Saml2Sp = new SamlServiceProvider { EntityId = "https://sp.example.com" },
        Saml2IdpEntityId = "https://idp.example.com"
    };

    [Fact]
    public async Task generates_a_bounded_32_char_hex_correlation_id_distinct_from_the_protected_handle()
    {
        var result = new Saml2LogoutPageResult(CreateRequest());

        await _subject.WriteHttpResponse(result, _context);

        _messageStore.Messages.Count.ShouldBe(1);
        var entry = _messageStore.Messages.Single();
        var protectedHandle = entry.Key;
        var correlationId = entry.Value.Data.SamlLogoutCorrelationId;

        correlationId.ShouldNotBeNullOrWhiteSpace();
        HexRegex.IsMatch(correlationId!).ShouldBeTrue();
        correlationId.ShouldNotBe(protectedHandle);
    }

    [Fact]
    public async Task logout_url_carries_the_protected_handle_not_the_correlation_id()
    {
        var result = new Saml2LogoutPageResult(CreateRequest());

        await _subject.WriteHttpResponse(result, _context);

        var entry = _messageStore.Messages.Single();
        var location = _context.Response.Headers.Location.ToString();

        location.ShouldContain($"logoutId={entry.Key}");
        location.ShouldNotContain(entry.Value.Data.SamlLogoutCorrelationId!);
    }

    [Fact]
    public async Task generates_a_fresh_correlation_id_for_every_invocation()
    {
        var result1 = new Saml2LogoutPageResult(CreateRequest());
        await _subject.WriteHttpResponse(result1, _context);

        var result2 = new Saml2LogoutPageResult(CreateRequest());
        await _subject.WriteHttpResponse(result2, _context);

        _messageStore.Messages.Count.ShouldBe(2);
        var ids = _messageStore.Messages.Values.Select(m => m.Data.SamlLogoutCorrelationId).ToList();
        ids[0].ShouldNotBe(ids[1]);
    }
}
