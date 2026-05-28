// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class Saml2SsoInteractionResponseGeneratorTests
{
    private const string Category = "Saml2SsoInteractionResponseGenerator";

    private static readonly SamlServiceProvider DefaultSp = new()
    {
        EntityId = "https://sp.example.com",
        DisplayName = "Test SP",
        Enabled = true,
    };

    private static Saml2SsoInteractionResponseGenerator CreateGenerator(MockProfileService? profileService = null) =>
        new(profileService ?? new MockProfileService(),
            NullLogger<Saml2SsoInteractionResponseGenerator>.Instance);

    private static ValidatedAuthnRequest CreateRequest(
        bool forceAuthn = false,
        bool isPassive = false,
        TrustLevel trustLevel = TrustLevel.None,
        ClaimsPrincipal? subject = null,
        SamlServiceProvider? sp = null)
        => new()
        {
            IdentityServerOptions = new IdentityServerOptions(),
            AuthnRequest = new AuthnRequest
            {
                ForceAuthn = forceAuthn,
                IsPassive = isPassive,
                TrustLevel = trustLevel,
                Issuer = new NameId("https://sp.example.com")
            },
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST",
            Saml2Message = new InboundSaml2Message
            {
                Name = "SAMLRequest",
                Xml = new System.Xml.XmlDocument().CreateElement("SAMLRequest"),
                Destination = "https://idp.example.com/Saml2/SSO",
                Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
            },
            Saml2IdpEntityId = "https://idp.example.com",
            Subject = subject,
            Saml2Sp = sp ?? DefaultSp,
        };

    private static ClaimsPrincipal AuthenticatedUser()
        => new(new ClaimsIdentity([new Claim("sub", "user1")], "test"));

    [Fact]
    [Trait("Category", Category)]
    public async Task UnauthenticatedUserReturnsLogin()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(subject: null);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnauthenticatedPassiveUserReturnsError()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(subject: null, isPassive: true);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsError.ShouldBeTrue();
        response.StatusCode.ShouldBe(SamlStatusCodes.Responder);
        response.SubStatusCode.ShouldBe(SamlStatusCodes.NoPassive);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AuthenticatedUserReturnsNoInteraction()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(subject: AuthenticatedUser(), forceAuthn: false);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeFalse();
        response.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task TrustedForceAuthnReturnsLogin()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(
            subject: AuthenticatedUser(),
            forceAuthn: true,
            trustLevel: TrustLevel.ConfiguredKey);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task TrustedForceAuthnAndPassiveReturnsError()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(
            subject: AuthenticatedUser(),
            forceAuthn: true,
            isPassive: true,
            trustLevel: TrustLevel.ConfiguredKey);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsError.ShouldBeTrue();
        response.StatusCode.ShouldBe(SamlStatusCodes.Responder);
        response.SubStatusCode.ShouldBe(SamlStatusCodes.NoPassive);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnsignedForceAuthnReturnsLogin()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(
            subject: AuthenticatedUser(),
            forceAuthn: true,
            trustLevel: TrustLevel.None);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task TlsTrustForceAuthnReturnsLogin()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(
            subject: AuthenticatedUser(),
            forceAuthn: true,
            trustLevel: TrustLevel.TLS);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task InactiveUserReturnsLogin()
    {
        var profileService = new MockProfileService { IsActive = false };
        var generator = CreateGenerator(profileService);
        var request = CreateRequest(subject: AuthenticatedUser());

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeTrue();
        profileService.IsActiveWasCalled.ShouldBeTrue();
        profileService.ActiveContext.Caller.ShouldBe(IdentityServerConstants.ProfileIsActiveCallers.SamlSsoEndpoint);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task InactivePassiveUserReturnsError()
    {
        var profileService = new MockProfileService { IsActive = false };
        var generator = CreateGenerator(profileService);
        var request = CreateRequest(subject: AuthenticatedUser(), isPassive: true);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsError.ShouldBeTrue();
        response.StatusCode.ShouldBe(SamlStatusCodes.Responder);
        response.SubStatusCode.ShouldBe(SamlStatusCodes.NoPassive);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ActiveUserPassesIsActiveCheckWithCorrectContext()
    {
        var profileService = new MockProfileService { IsActive = true };
        var generator = CreateGenerator(profileService);
        var request = CreateRequest(subject: AuthenticatedUser());

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeFalse();
        response.IsError.ShouldBeFalse();
        profileService.IsActiveWasCalled.ShouldBeTrue();
        profileService.ActiveContext.Application.ShouldBe(DefaultSp);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnauthenticatedPrincipalReturnsLogin()
    {
        // Subject is non-null but the identity is not authenticated
        var unauthenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")]));
        var profileService = new MockProfileService();
        var generator = CreateGenerator(profileService);
        var request = CreateRequest(subject: unauthenticatedPrincipal);

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeTrue();
        profileService.IsActiveWasCalled.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NullSaml2SpReturnsLogin()
    {
        var profileService = new MockProfileService();
        var generator = CreateGenerator(profileService);
        var request = CreateRequest(subject: AuthenticatedUser(), sp: null);
        request.Saml2Sp = null;

        var response = await generator.ProcessInteractionAsync(request, CancellationToken.None);

        response.IsLogin.ShouldBeTrue();
        profileService.IsActiveWasCalled.ShouldBeFalse();
    }
}
