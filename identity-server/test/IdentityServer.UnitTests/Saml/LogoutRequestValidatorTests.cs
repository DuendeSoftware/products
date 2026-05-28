// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;
using SamlLogoutRequest = Duende.IdentityServer.Saml.Samlp.LogoutRequest;

namespace UnitTests.Saml;

public sealed class LogoutRequestValidatorTests
{
    private const string Category = "LogoutRequest Validator";
    private const string IdpOrigin = "https://idp.example.com";
    private const string SloPath = "/Saml2/SLO";
    private const string ExpectedDestination = IdpOrigin + SloPath;
    private const string SpEntityId = "https://sp.example.com";
    private const string SpSloUrl = "https://sp.example.com/slo";

    private static readonly DateTimeOffset DefaultNow = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidRequestSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedLogoutRequest(trustLevel: TrustLevel.ConfiguredKey);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnknownSpEntityIdFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            issuerEntityId: "https://unknown.example.com");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DisabledSpFails()
    {
        var sp = CreateDefaultSp();
        sp.Enabled = false;
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedLogoutRequest(trustLevel: TrustLevel.ConfiguredKey);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpWithoutSingleLogoutServiceUrlFails()
    {
        var sp = CreateDefaultSp();
        sp.SingleLogoutServiceUrls.Clear();
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedLogoutRequest(trustLevel: TrustLevel.ConfiguredKey);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnsignedRequestFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedLogoutRequest(trustLevel: TrustLevel.None);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task WrongVersionFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            version: "1.1");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.VersionMismatch);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ExpiredNotOnOrAfterFails()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var validator = CreateValidator(timeProvider: timeProvider);
        var expiredTime = new DateTime(2025, 6, 15, 11, 0, 0, DateTimeKind.Utc); // 1 hour ago
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            notOnOrAfter: expiredTime);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidNotOnOrAfterSucceeds()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var validator = CreateValidator(timeProvider: timeProvider);
        var futureTime = new DateTime(2025, 6, 15, 12, 5, 0, DateTimeKind.Utc); // 5 min in future
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            notOnOrAfter: futureTime);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MissingDestinationOnSignedRequestFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            destination: null);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionValidation_SkippedWhenNoAuthenticatedUser()
    {
        var validator = CreateValidator();
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            subject: null);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionValidation_MatchingSessionSucceeds()
    {
        var session = new MockUserSession();
        session.SamlSessions.Add(new SamlSpSessionData
        {
            EntityId = SpEntityId,
            NameId = "user@example.com",
            SessionIndex = "_session-123"
        });
        var validator = CreateValidator(userSession: session);
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            nameId: "user@example.com",
            sessionIndex: "_session-123",
            subject: CreateAuthenticatedPrincipal());

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionValidation_NoSessionForSpReturnsValidWithNoSessionFound()
    {
        var session = new MockUserSession();
        // No sessions added
        var validator = CreateValidator(userSession: session);
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            nameId: "user@example.com",
            subject: CreateAuthenticatedPrincipal());

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.SessionFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionValidation_MismatchedNameIdFails()
    {
        var session = new MockUserSession();
        session.SamlSessions.Add(new SamlSpSessionData
        {
            EntityId = SpEntityId,
            NameId = "other-user@example.com",
            SessionIndex = "_session-123"
        });
        var validator = CreateValidator(userSession: session);
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            nameId: "user@example.com",
            subject: CreateAuthenticatedPrincipal());

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("NameID does not match");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionValidation_MismatchedSessionIndexReturnsValidWithNoSessionFound()
    {
        var session = new MockUserSession();
        session.SamlSessions.Add(new SamlSpSessionData
        {
            EntityId = SpEntityId,
            NameId = "user@example.com",
            SessionIndex = "_session-123"
        });
        var validator = CreateValidator(userSession: session);
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            nameId: "user@example.com",
            sessionIndex: "_wrong-session",
            subject: CreateAuthenticatedPrincipal());

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.SessionFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionValidation_NoSessionIndexInRequest_MatchesOnNameIdOnly()
    {
        var session = new MockUserSession();
        session.SamlSessions.Add(new SamlSpSessionData
        {
            EntityId = SpEntityId,
            NameId = "user@example.com",
            SessionIndex = "_session-123"
        });
        var validator = CreateValidator(userSession: session);
        var request = CreateValidatedLogoutRequest(
            trustLevel: TrustLevel.ConfiguredKey,
            nameId: "user@example.com",
            sessionIndex: null,
            subject: CreateAuthenticatedPrincipal());

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    private static SamlServiceProvider CreateDefaultSp() => new()
    {
        EntityId = SpEntityId,
        Enabled = true,
        SingleLogoutServiceUrls = [new SamlEndpointType { Location = SpSloUrl, Binding = SamlBinding.HttpRedirect }]
    };

    private static LogoutRequestValidator CreateValidator(
        SamlServiceProvider? sp = null,
        TimeProvider? timeProvider = null,
        SamlOptions? samlOptions = null,
        IUserSession? userSession = null)
    {
        sp ??= CreateDefaultSp();
        var store = new InMemorySamlServiceProviderStore([sp]);
        var serverUrls = new MockServerUrls { Origin = IdpOrigin };
        samlOptions ??= new SamlOptions();
        samlOptions.Endpoints.SingleLogoutServicePath = SloPath;
        var identityServerOptions = new IdentityServerOptions { Saml = samlOptions };
        return new LogoutRequestValidator(
            store,
            userSession ?? new MockUserSession(),
            timeProvider ?? new FakeTimeProvider(DefaultNow),
            Microsoft.Extensions.Options.Options.Create(identityServerOptions),
            serverUrls,
            NullLogger<LogoutRequestValidator>.Instance);
    }

    private static ValidatedLogoutRequest CreateValidatedLogoutRequest(
        TrustLevel trustLevel = TrustLevel.ConfiguredKey,
        string version = SamlVersions.V2,
        string? destination = ExpectedDestination,
        string? issuerEntityId = SpEntityId,
        DateTime? notOnOrAfter = null,
        string requestId = "_test-request-id",
        string nameId = "user@example.com",
        string? sessionIndex = null,
        ClaimsPrincipal? subject = null)
    {
        var xmlDoc = new XmlDocument();
        var xmlElement = xmlDoc.CreateElement("SAMLRequest");

        return new ValidatedLogoutRequest
        {
            LogoutRequest = new SamlLogoutRequest
            {
                Id = requestId,
                Version = version,
                IssueInstant = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                Issuer = issuerEntityId != null ? new NameId(issuerEntityId) : null,
                Destination = destination,
                TrustLevel = trustLevel,
                NotOnOrAfter = notOnOrAfter.HasValue ? (Duende.IdentityServer.Saml.Common.DateTimeUtc)notOnOrAfter.Value : null,
                NameId = new NameId(nameId),
                SessionIndex = sessionIndex
            },
            Binding = SamlConstants.Bindings.HttpRedirect,
            Saml2Message = new InboundSaml2Message
            {
                Name = "SAMLRequest",
                Xml = xmlElement,
                Destination = ExpectedDestination,
                Binding = SamlConstants.Bindings.HttpRedirect
            },
            Saml2IdpEntityId = "https://idp.example.com",
            Subject = subject
        };
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal()
        => new(new ClaimsIdentity([new Claim("sub", "user-123")], "test"));

}
