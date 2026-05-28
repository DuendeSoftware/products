// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class SamlReturnUrlParserTests
{
    private readonly InMemorySamlSigninStateStore _stateStore = new(TimeProvider.System, NullLogger<InMemorySamlSigninStateStore>.Instance);
    private readonly InMemorySamlServiceProviderStore _spStore;
    private readonly SamlReturnUrlParser _subject;
    private readonly SamlServiceProvider _sp = new() { EntityId = "https://sp.example.com", DisplayName = "Test SP", Enabled = true };

    private static readonly IndexedEndpoint DefaultAcs = new()
    {
        Binding = SamlBinding.HttpPost,
        Location = "https://sp.example.com/acs"
    };

    public SamlReturnUrlParserTests()
    {
        _spStore = new InMemorySamlServiceProviderStore([_sp]);

        _subject = new SamlReturnUrlParser(
            _stateStore,
            _spStore,
            Options.Create(new IdentityServerOptions()),
            new MockServerUrls { Origin = "https://idp.example.com" },
            TestLogger.Create<SamlReturnUrlParser>());
    }

    [Fact]
    public void IsValidReturnUrl_ReturnsTrueForSamlCallbackUrl() =>
        _subject.IsValidReturnUrl("/Saml2/SSO/Callback?samlStateId=" + Guid.NewGuid()).ShouldBeTrue();

    [Fact]
    public void IsValidReturnUrl_ReturnsFalseForOidcUrl() =>
        _subject.IsValidReturnUrl("/connect/authorize/callback?client_id=foo").ShouldBeFalse();

    [Fact]
    public void IsValidReturnUrl_ReturnsFalseWhenMissingStateId() =>
        _subject.IsValidReturnUrl("/Saml2/SSO/Callback").ShouldBeFalse();

    [Fact]
    public void IsValidReturnUrl_ReturnsFalseForAbsoluteUrl() =>
        _subject.IsValidReturnUrl("https://evil.com/Saml2/SSO/Callback?samlStateId=" + Guid.NewGuid()).ShouldBeFalse();

    [Fact]
    public async Task ParseAsync_ReturnsNullForNonSamlUrl()
    {
        var result = await _subject.ParseAsync("/connect/authorize/callback", CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ParseAsync_ReturnsNullWhenStateNotFound()
    {
        var url = "/Saml2/SSO/Callback?samlStateId=" + Guid.NewGuid();
        var result = await _subject.ParseAsync(url, CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ParseAsync_ReturnsSamlAuthenticationContextWithCorrectServiceProvider()
    {
        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs
        });

        var result = await _subject.ParseAsync(CallbackUrl(stateId), CancellationToken.None);

        result.ShouldNotBeNull();
        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.ServiceProvider.EntityId.ShouldBe(_sp.EntityId);
    }

    [Fact]
    public async Task ParseAsync_WithForceAuthn_IncludesLoginInPromptModes()
    {
        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs,
            AuthnRequestData = new StoredAuthnRequestData { ForceAuthn = true }
        });

        var result = await _subject.ParseAsync(CallbackUrl(stateId), CancellationToken.None);

        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.PromptModes.ShouldContain("login");
    }

    [Fact]
    public async Task ParseAsync_WithIsPassive_IncludesNoneInPromptModes()
    {
        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs,
            AuthnRequestData = new StoredAuthnRequestData { IsPassive = true }
        });

        var result = await _subject.ParseAsync(CallbackUrl(stateId), CancellationToken.None);

        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.PromptModes.ShouldContain("none");
    }

    [Fact]
    public async Task ParseAsync_WithTenantInRequestedAuthnContext_ReturnsTenant()
    {
        var authnContext = new StoredAuthnRequestData
        {
            RequestedAuthnContext = new StoredRequestedAuthnContext()
        };
        authnContext.RequestedAuthnContext.AuthnContextClassRef.Add("tenant:acme");

        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs,
            AuthnRequestData = authnContext
        });

        var result = await _subject.ParseAsync(CallbackUrl(stateId), CancellationToken.None);

        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.Tenant.ShouldBe("acme");
    }

    [Fact]
    public async Task ParseAsync_WithScopingIdpList_ReturnsFirstIdP()
    {
        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs,
            AuthnRequestData = new StoredAuthnRequestData { IdpHintProviderId = "https://idp.example.com" }
        });

        var result = await _subject.ParseAsync(CallbackUrl(stateId), CancellationToken.None);

        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.IdP.ShouldBe("https://idp.example.com");
    }

    [Fact]
    public async Task ParseAsync_WithSubjectNameId_ReturnsLoginHint()
    {
        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs,
            AuthnRequestData = new StoredAuthnRequestData
            {
                SubjectNameIdValue = "alice@example.com"
            }
        });

        var result = await _subject.ParseAsync(CallbackUrl(stateId), CancellationToken.None);

        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.LoginHint.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task ParseAsync_IdpInitiated_ReturnsNullForIdpLoginHintAndTenant()
    {
        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs,
            IsIdpInitiated = true,
            AuthnRequestData = null
        });

        var result = await _subject.ParseAsync(CallbackUrl(stateId), CancellationToken.None);

        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.IdP.ShouldBeNull();
        samlResult.LoginHint.ShouldBeNull();
        samlResult.Tenant.ShouldBeNull();
        samlResult.PromptModes.ShouldBeEmpty();
        samlResult.IsIdpInitiated.ShouldBeTrue();
    }

    private async Task<Guid> StoreState(SamlAuthenticationState state)
    {
        state.CreatedUtc = DateTimeOffset.UtcNow;
        state.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
        return await _stateStore.StoreSigninRequestStateAsync(state, CancellationToken.None);
    }

    private static string CallbackUrl(Guid stateId) =>
        $"/Saml2/SSO/Callback?samlStateId={stateId}";

    private SamlReturnUrlParser CreateParserWithAllowOrigin(bool allowOrigin = true, string origin = "https://idp.example.com")
    {
        var identityServerOptions = new IdentityServerOptions();
        identityServerOptions.UserInteraction.AllowOriginInReturnUrl = allowOrigin;

        return new SamlReturnUrlParser(
            _stateStore,
            _spStore,
            Options.Create(identityServerOptions),
            new MockServerUrls { Origin = origin },
            TestLogger.Create<SamlReturnUrlParser>());
    }

    [Fact]
    public void IsValidReturnUrl_WithAllowOrigin_AcceptsSameOriginAbsoluteUrl()
    {
        var parser = CreateParserWithAllowOrigin();
        var url = "https://idp.example.com/Saml2/SSO/Callback?samlStateId=" + Guid.NewGuid();
        parser.IsValidReturnUrl(url).ShouldBeTrue();
    }

    [Fact]
    public void IsValidReturnUrl_WithAllowOrigin_RejectsDifferentOriginAbsoluteUrl()
    {
        var parser = CreateParserWithAllowOrigin();
        var url = "https://evil.com/Saml2/SSO/Callback?samlStateId=" + Guid.NewGuid();
        parser.IsValidReturnUrl(url).ShouldBeFalse();
    }

    [Fact]
    public void IsValidReturnUrl_WithoutAllowOrigin_RejectsSameOriginAbsoluteUrl()
    {
        var parser = CreateParserWithAllowOrigin(allowOrigin: false);
        var url = "https://idp.example.com/Saml2/SSO/Callback?samlStateId=" + Guid.NewGuid();
        parser.IsValidReturnUrl(url).ShouldBeFalse();
    }

    [Fact]
    public async Task ParseAsync_WithAllowOrigin_ParsesSameOriginAbsoluteUrl()
    {
        var parser = CreateParserWithAllowOrigin();

        var stateId = await StoreState(new SamlAuthenticationState
        {
            ServiceProviderEntityId = _sp.EntityId,
            AssertionConsumerService = DefaultAcs,
        });

        var url = $"https://idp.example.com/Saml2/SSO/Callback?samlStateId={stateId}";
        var result = await parser.ParseAsync(url, CancellationToken.None);

        result.ShouldNotBeNull();
        var samlResult = result.ShouldBeOfType<SamlAuthenticationContext>();
        samlResult.ServiceProvider.EntityId.ShouldBe(_sp.EntityId);
    }
}
