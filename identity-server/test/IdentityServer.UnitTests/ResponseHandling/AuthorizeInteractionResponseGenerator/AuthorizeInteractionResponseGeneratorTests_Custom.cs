// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Shouldly;
using UnitTests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using static Duende.IdentityModel.OidcConstants;

namespace UnitTests.ResponseHandling.AuthorizeInteractionResponseGenerator;

public class CustomAuthorizeInteractionResponseGenerator : Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator
{
    public CustomAuthorizeInteractionResponseGenerator(
        IdentityServerOptions options,
        IClock clock, 
        ILogger<Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator> logger, 
        IConsentService consent, 
        IProfileService profile) : base(options, clock, logger, consent, profile)
    {
    }

    public InteractionResponse ProcessLoginResponse { get; set; }
    protected internal override Task<InteractionResponse> ProcessLoginAsync(ValidatedAuthorizeRequest request)
    {
        if (ProcessLoginResponse != null)
        {
            return Task.FromResult(ProcessLoginResponse);
        }

        return base.ProcessLoginAsync(request);
    }

    public InteractionResponse ProcessConsentResponse { get; set; }
    protected internal override Task<InteractionResponse> ProcessConsentAsync(ValidatedAuthorizeRequest request, ConsentResponse consent = null)
    {
        if (ProcessConsentResponse != null)
        {
            return Task.FromResult(ProcessConsentResponse);
        }
        return base.ProcessConsentAsync(request, consent);
    }
}

public class AuthorizeInteractionResponseGeneratorTests_Custom
{
    private IdentityServerOptions _options = new IdentityServerOptions();
    private CustomAuthorizeInteractionResponseGenerator _subject;
    private MockConsentService _mockConsentService = new MockConsentService();
    private StubClock _clock = new StubClock();

    public AuthorizeInteractionResponseGeneratorTests_Custom()
    {
        _subject = new CustomAuthorizeInteractionResponseGenerator(
            _options,
            _clock,
            TestLogger.Create<Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator>(),
            _mockConsentService,
            new MockProfileService());
    }


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

        var result = await _subject.ProcessInteractionAsync(request);

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

        var result = await _subject.ProcessInteractionAsync(request);

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

        var result = await _subject.ProcessInteractionAsync(request);

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

        var result = await _subject.ProcessInteractionAsync(request);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("consent_required");
    }
}