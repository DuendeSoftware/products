// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;
using static Duende.IdentityModel.OidcConstants;

namespace UnitTests.ResponseHandling.AuthorizeInteractionResponseGenerator;

public class CustomAuthorizeInteractionResponseGenerator : Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator
{
    public CustomAuthorizeInteractionResponseGenerator(
        IdentityServerOptions options,
        TimeProvider timeProvider,
        ILogger<Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator> logger,
        IConsentService consent,
        IProfileService profile) : base(options, timeProvider, logger, consent, profile)
    {
    }

    public InteractionResponse ProcessLoginResponse { get; set; }
    protected internal override Task<InteractionResponse> ProcessLoginAsync(ValidatedAuthorizeRequest request, Ct ct)
    {
        if (ProcessLoginResponse != null)
        {
            return Task.FromResult(ProcessLoginResponse);
        }

        return base.ProcessLoginAsync(request, ct);
    }

    public InteractionResponse ProcessConsentResponse { get; set; }
    protected internal override Task<InteractionResponse> ProcessConsentAsync(ValidatedAuthorizeRequest request, ConsentResponse consent, Ct ct)
    {
        if (ProcessConsentResponse != null)
        {
            return Task.FromResult(ProcessConsentResponse);
        }
        return base.ProcessConsentAsync(request, consent, ct);
    }
}

public class AuthorizeInteractionResponseGeneratorTests_Custom
{
    private IdentityServerOptions _options = new IdentityServerOptions();
    private CustomAuthorizeInteractionResponseGenerator _subject;
    private MockConsentService _mockConsentService = new MockConsentService();
    private FakeTimeProvider _timeProvider = new FakeTimeProvider();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public AuthorizeInteractionResponseGeneratorTests_Custom() => _subject = new CustomAuthorizeInteractionResponseGenerator(
            _options,
            _timeProvider,
            TestLogger.Create<Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator>(),
            _mockConsentService,
            new MockProfileService());


    [Fact]
    public async Task ProcessInteractionAsync_with_overridden_login_returns_redirect_should_return_redirect()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "foo",
            Subject = new IdentityServerUser("123")
            {
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider
            }.CreatePrincipal(),
            Client = new Client
            {
            },
        };

        _subject.ProcessLoginResponse = new InteractionResponse
        {
            RedirectUrl = "/custom"
        };

        var result = await _subject.ProcessInteractionAsync(request, null, _ct);

        result.IsRedirect.ShouldBeTrue();
        result.RedirectUrl.ShouldBe("/custom");
    }

    [Fact]
    public async Task ProcessInteractionAsync_with_prompt_none_and_login_returns_login_should_return_error()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "foo",
            Subject = new IdentityServerUser("123")
            {
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider
            }.CreatePrincipal(),
            Client = new Client
            {
            },
            PromptModes = new[] { PromptModes.None },
        };

        _subject.ProcessLoginResponse = new InteractionResponse
        {
            IsLogin = true
        };

        var result = await _subject.ProcessInteractionAsync(request, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("login_required");
    }

    [Fact]
    public async Task ProcessInteractionAsync_with_prompt_none_and_login_returns_redirect_should_return_error()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "foo",
            Subject = new IdentityServerUser("123")
            {
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider
            }.CreatePrincipal(),
            Client = new Client
            {
            },
            PromptModes = new[] { PromptModes.None },
        };

        _subject.ProcessLoginResponse = new InteractionResponse
        {
            RedirectUrl = "/custom"
        };

        var result = await _subject.ProcessInteractionAsync(request, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("interaction_required");
        result.RedirectUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessInteractionAsync_with_prompt_none_and_consent_returns_consent_should_return_error()
    {
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = "foo",
            Subject = new IdentityServerUser("123")
            {
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider
            }.CreatePrincipal(),
            Client = new Client
            {
            },
            PromptModes = new[] { PromptModes.None },
        };

        _subject.ProcessConsentResponse = new InteractionResponse
        {
            IsConsent = true
        };

        var result = await _subject.ProcessInteractionAsync(request, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("consent_required");
    }
}
