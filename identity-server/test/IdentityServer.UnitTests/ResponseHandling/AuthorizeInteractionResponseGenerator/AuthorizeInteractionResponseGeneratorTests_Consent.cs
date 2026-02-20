// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;

namespace UnitTests.ResponseHandling.AuthorizeInteractionResponseGenerator;

public class AuthorizeInteractionResponseGeneratorTests_Consent
{
    private Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator _subject;
    private IdentityServerOptions _options = new IdentityServerOptions();
    private MockConsentService _mockConsent = new MockConsentService();
    private MockProfileService _fakeUserService = new MockProfileService();
    private readonly CT _ct = TestContext.Current.CancellationToken;

    private void RequiresConsent(bool value) => _mockConsent.RequiresConsentResult = value;

    private void AssertUpdateConsentNotCalled()
    {
        _mockConsent.ConsentClient.ShouldBeNull();
        _mockConsent.ConsentSubject.ShouldBeNull();
        _mockConsent.ConsentScopes.ShouldBeNull();
    }

    private void AssertUpdateConsentCalled(Client client, ClaimsPrincipal user, params string[] scopes)
    {
        _mockConsent.ConsentClient.ShouldBeSameAs(client);
        _mockConsent.ConsentSubject.ShouldBeSameAs(user);
        _mockConsent.ConsentScopes.ShouldBe(scopes);
    }

    private static IEnumerable<IdentityResource> GetIdentityScopes() => new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        };

    private static IEnumerable<ApiResource> GetApiResources() => new ApiResource[]
        {
            new ApiResource
            {
                Name = "api",
                Scopes = { "read", "write", "forbidden" }
            }
        };

    private static IEnumerable<ApiScope> GetScopes() => new ApiScope[]
        {
            new ApiScope
            {
                Name = "read",
                DisplayName = "Read data",
                Emphasize = false
            },
            new ApiScope
            {
                Name = "write",
                DisplayName = "Write data",
                Emphasize = true
            },
            new ApiScope
            {
                Name = "forbidden",
                DisplayName = "Forbidden scope",
                Emphasize = true
            }
        };

    public AuthorizeInteractionResponseGeneratorTests_Consent() => _subject = new Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator(
            _options,
            new FakeTimeProvider(),
            TestLogger.Create<Duende.IdentityServer.ResponseHandling.AuthorizeInteractionResponseGenerator>(),
            _mockConsent,
            _fakeUserService);

    private static ResourceValidationResult GetValidatedResources(params string[] scopes)
    {
        var resources = new Resources(GetIdentityScopes(), GetApiResources(), GetScopes());
        return new ResourceValidationResult(resources).Filter(scopes);
    }


    [Fact]
    public async Task ProcessConsentAsync_NullRequest_Throws()
    {
        Func<Task> act = () => _subject.ProcessConsentAsync(null, new ConsentResponse(), _ct);

        var exception = await act.ShouldThrowAsync<ArgumentNullException>();
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ProcessConsentAsync_AllowsNullConsent()
    {
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            PromptModes = new[] { OidcConstants.PromptModes.Consent },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        await _subject.ProcessConsentAsync(request, null, _ct);
    }

    [Fact]
    public async Task ProcessConsentAsync_PromptModeIsLogin_Throws()
    {
        RequiresConsent(true);
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            PromptModes = new[] { OidcConstants.PromptModes.Login },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };

        Func<Task> act = () => _subject.ProcessConsentAsync(request, null, _ct);

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.Message.ShouldMatch(".*PromptMode.*");
    }

    [Fact]
    public async Task ProcessConsentAsync_PromptModeIsSelectAccount_Throws()
    {
        RequiresConsent(true);
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            PromptModes = new[] { OidcConstants.PromptModes.SelectAccount },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };

        Func<Task> act = () => _subject.ProcessConsentAsync(request, null, _ct);

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.Message.ShouldMatch(".*PromptMode.*");
    }


    [Fact]
    public async Task ProcessConsentAsync_RequiresConsentButPromptModeIsNone_ReturnsErrorResult()
    {
        RequiresConsent(true);
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            PromptModes = new[] { OidcConstants.PromptModes.None },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var result = await _subject.ProcessConsentAsync(request, null, _ct);

        request.WasConsentShown.ShouldBeFalse();
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.ConsentRequired);
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_PromptModeIsConsent_NoPriorConsent_ReturnsConsentResult()
    {
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            PromptModes = new[] { OidcConstants.PromptModes.Consent },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var result = await _subject.ProcessConsentAsync(request, null, _ct);
        request.WasConsentShown.ShouldBeFalse();
        result.IsConsent.ShouldBeTrue();
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_NoPromptMode_ConsentServiceRequiresConsent_NoPriorConsent_ReturnsConsentResult()
    {
        RequiresConsent(true);
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            PromptModes = new[] { OidcConstants.PromptModes.Consent },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var result = await _subject.ProcessConsentAsync(request, null, _ct);
        request.WasConsentShown.ShouldBeFalse();
        result.IsConsent.ShouldBeTrue();
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_PromptModeIsConsent_ConsentNotGranted_ReturnsErrorResult()
    {
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            PromptModes = new[] { OidcConstants.PromptModes.Consent },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };

        var consent = new ConsentResponse
        {
            RememberConsent = false,
            ScopesValuesConsented = new string[] { }
        };
        var result = await _subject.ProcessConsentAsync(request, consent, _ct);
        request.WasConsentShown.ShouldBeTrue();
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.AccessDenied);
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_NoPromptMode_ConsentServiceRequiresConsent_ConsentNotGranted_ReturnsErrorResult()
    {
        RequiresConsent(true);
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var consent = new ConsentResponse
        {
            RememberConsent = false,
            ScopesValuesConsented = new string[] { }
        };
        var result = await _subject.ProcessConsentAsync(request, consent, _ct);
        request.WasConsentShown.ShouldBeTrue();
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.AccessDenied);
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_NoPromptMode_ConsentServiceRequiresConsent_ConsentGrantedButMissingRequiredScopes_ReturnsErrorResult()
    {
        RequiresConsent(true);
        var client = new Client { };
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
            Client = client
        };

        var consent = new ConsentResponse
        {
            RememberConsent = false,
            ScopesValuesConsented = new string[] { "read" }
        };

        var result = await _subject.ProcessConsentAsync(request, consent, _ct);
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.AccessDenied);
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_NoPromptMode_ConsentServiceRequiresConsent_ConsentGranted_ScopesSelected_ReturnsConsentResult()
    {
        RequiresConsent(true);
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            Client = new Client
            {
                AllowRememberConsent = false
            },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var consent = new ConsentResponse
        {
            RememberConsent = false,
            ScopesValuesConsented = new string[] { "openid", "read" }
        };
        var result = await _subject.ProcessConsentAsync(request, consent, _ct);
        request.ValidatedResources.Resources.IdentityResources.Count.ShouldBe(1);
        request.ValidatedResources.Resources.ApiScopes.Count.ShouldBe(1);
        "openid".ShouldBe(request.ValidatedResources.Resources.IdentityResources.Select(x => x.Name).First());
        "read".ShouldBe(request.ValidatedResources.Resources.ApiScopes.First().Name);
        request.WasConsentShown.ShouldBeTrue();
        result.IsConsent.ShouldBeFalse();
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_PromptModeConsent_ConsentGranted_ScopesSelected_ReturnsConsentResult()
    {
        RequiresConsent(true);
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            Client = new Client
            {
                AllowRememberConsent = false
            },
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var consent = new ConsentResponse
        {
            RememberConsent = false,
            ScopesValuesConsented = new string[] { "openid", "read" }
        };
        var result = await _subject.ProcessConsentAsync(request, consent, _ct);
        request.ValidatedResources.Resources.IdentityResources.Count.ShouldBe(1);
        request.ValidatedResources.Resources.ApiScopes.Count.ShouldBe(1);
        "read".ShouldBe(request.ValidatedResources.Resources.ApiScopes.First().Name);
        request.WasConsentShown.ShouldBeTrue();
        result.IsConsent.ShouldBeFalse();
        AssertUpdateConsentNotCalled();
    }

    [Fact]
    public async Task ProcessConsentAsync_AllowConsentSelected_SavesConsent()
    {
        RequiresConsent(true);
        var client = new Client { AllowRememberConsent = true };
        var user = new ClaimsPrincipal();
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            Client = client,
            Subject = user,
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var consent = new ConsentResponse
        {
            RememberConsent = true,
            ScopesValuesConsented = new string[] { "openid", "read" }
        };
        var result = await _subject.ProcessConsentAsync(request, consent, _ct);
        AssertUpdateConsentCalled(client, user, "openid", "read");
    }

    [Fact]
    public async Task ProcessConsentAsync_NotRememberingConsent_DoesNotSaveConsent()
    {
        RequiresConsent(true);
        var client = new Client { AllowRememberConsent = true };
        var user = new ClaimsPrincipal();
        var request = new ValidatedAuthorizeRequest()
        {
            ResponseMode = OidcConstants.ResponseModes.Fragment,
            State = "12345",
            RedirectUri = "https://client.com/callback",
            Client = client,
            Subject = user,
            RequestedScopes = new List<string> { "openid", "read", "write" },
            ValidatedResources = GetValidatedResources("openid", "read", "write"),
        };
        var consent = new ConsentResponse
        {
            RememberConsent = false,
            ScopesValuesConsented = new string[] { "openid", "read" }
        };
        var result = await _subject.ProcessConsentAsync(request, consent, _ct);
        AssertUpdateConsentCalled(client, user);
    }
}
