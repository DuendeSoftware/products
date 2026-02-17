// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultConsentServiceTests
{
    private DefaultConsentService _subject;
    private MockProfileService _mockMockProfileService = new MockProfileService();

    private ClaimsPrincipal _user;
    private Client _client;
    private TestUserConsentStore _userConsentStore = new TestUserConsentStore();
    private FakeTimeProvider _clock = new FakeTimeProvider();

    private DateTime now;

    public DefaultConsentServiceTests()
    {
        _clock.SetUtcNow(UtcNow);

        _client = new Client
        {
            ClientId = "client",
            RequireConsent = true,
            RequirePkce = false
        };

        _user = new IdentityServerUser("bob")
        {
            AdditionalClaims =
            {
                new Claim("foo", "foo1"),
                new Claim("foo", "foo2"),
                new Claim("bar", "bar1"),
                new Claim("bar", "bar2"),
                new Claim(JwtClaimTypes.AuthenticationContextClassReference, "acr1")
            }
        }.CreatePrincipal();

        _subject = new DefaultConsentService(_clock, _userConsentStore, TestLogger.Create<DefaultConsentService>());
    }

    public DateTime UtcNow
    {
        get
        {
            if (now > DateTime.MinValue)
            {
                return now;
            }

            return DateTime.UtcNow;
        }
    }

    [Fact]
    public async Task UpdateConsentAsync_when_client_does_not_allow_remember_consent_should_not_update_store()
    {
        _client.AllowRememberConsent = false;

        await _subject.UpdateConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        var consent = await _userConsentStore.GetUserConsentAsync(_user.GetSubjectId(), _client.ClientId);
        consent.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateConsentAsync_should_persist_consent()
    {
        await _subject.UpdateConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        var consent = await _userConsentStore.GetUserConsentAsync(_user.GetSubjectId(), _client.ClientId);
        consent.Scopes.Count().ShouldBe(2);
        consent.Scopes.ShouldContain("scope1");
        consent.Scopes.ShouldContain("scope2");
    }

    [Fact]
    public async Task UpdateConsentAsync_empty_scopes_should_remove_consent()
    {
        await _subject.UpdateConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        await _subject.UpdateConsentAsync(_user, _client, new ParsedScopeValue[] { });

        var consent = await _userConsentStore.GetUserConsentAsync(_user.GetSubjectId(), _client.ClientId);
        consent.ShouldBeNull();
    }

    [Fact]
    public async Task RequiresConsentAsync_client_does_not_require_consent_should_not_require_consent()
    {
        _client.RequireConsent = false;

        var result = await _subject.RequiresConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RequiresConsentAsync_client_does_not_allow_remember_consent_should_require_consent()
    {
        _client.AllowRememberConsent = false;

        var result = await _subject.RequiresConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RequiresConsentAsync_no_scopes_should_not_require_consent()
    {
        var result = await _subject.RequiresConsentAsync(_user, _client, new ParsedScopeValue[] { });

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RequiresConsentAsync_offline_access_should_require_consent()
    {
        var result = await _subject.RequiresConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("offline_access") });

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RequiresConsentAsync_no_prior_consent_should_require_consent()
    {
        var result = await _subject.RequiresConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RequiresConsentAsync_prior_consent_should_not_require_consent()
    {
        await _subject.UpdateConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        var result = await _subject.RequiresConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RequiresConsentAsync_prior_consent_with_more_scopes_should_not_require_consent()
    {
        await _subject.UpdateConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2"), new ParsedScopeValue("scope3") });

        var result = await _subject.RequiresConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope2") });

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RequiresConsentAsync_prior_consent_with_too_few_scopes_should_require_consent()
    {
        await _subject.UpdateConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope2"), new ParsedScopeValue("scope3") });

        var result = await _subject.RequiresConsentAsync(_user, _client, new[] { new ParsedScopeValue("scope1"), new ParsedScopeValue("scope2") });

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RequiresConsentAsync_expired_consent_should_require_consent()
    {
        now = DateTime.UtcNow;

        var scopes = new[] { new ParsedScopeValue("foo"), new ParsedScopeValue("bar") };
        _client.ConsentLifetime = 2;

        await _subject.UpdateConsentAsync(_user, _client, scopes);

        now = now.AddSeconds(3);

        var result = await _subject.RequiresConsentAsync(_user, _client, scopes);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RequiresConsentAsync_expired_consent_should_remove_consent()
    {
        now = DateTime.UtcNow;

        var scopes = new[] { new ParsedScopeValue("foo"), new ParsedScopeValue("bar") };
        _client.ConsentLifetime = 2;

        await _subject.UpdateConsentAsync(_user, _client, scopes);

        now = now.AddSeconds(3);

        await _subject.RequiresConsentAsync(_user, _client, scopes);

        var result = await _userConsentStore.GetUserConsentAsync(_user.GetSubjectId(), _client.ClientId);

        result.ShouldBeNull();
    }
}
