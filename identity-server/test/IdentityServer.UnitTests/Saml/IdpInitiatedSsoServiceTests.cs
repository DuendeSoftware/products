// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using System.Xml;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Services.Default;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class IdpInitiatedSsoServiceTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private const string SpEntityId = "https://sp.example.com";
    private const string SpAcsUrl = "https://sp.example.com/acs";
    private const string IdpEntityId = "https://idp.example.com";

    [Fact]
    public async Task ReturnsErrorWhenSpEntityIdIsNull()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), null!, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("spEntityId");
    }

    [Fact]
    public async Task ReturnsErrorWhenSpEntityIdIsEmpty()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), "", null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("spEntityId");
    }

    [Fact]
    public async Task ReturnsErrorWhenSpNotFound()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(
            CreateAuthenticatedContext(), "https://unknown.example.com", null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("not found");
        // Error message must NOT embed the raw entity ID (security: fixed strings only)
        result.Error!.ShouldNotContain("https://unknown.example.com");
        result.SpEntityId.ShouldBe("https://unknown.example.com");
    }

    [Fact]
    public async Task ReturnsErrorWhenSpIsDisabled()
    {
        var sp = CreateSp(enabled: false);
        var svc = CreateService(spStore: new StubSpStore(sp));

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("disabled");
        result.Error!.ShouldNotContain(SpEntityId);
        result.SpEntityId.ShouldBe(SpEntityId);
    }

    [Fact]
    public async Task ReturnsErrorWhenIdpInitiatedNotAllowed()
    {
        var sp = CreateSp(allowIdpInitiated: false);
        var svc = CreateService(spStore: new InMemorySamlServiceProviderStore([sp]));

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("does not allow IdP-initiated SSO");
        result.Error!.ShouldNotContain(SpEntityId);
        result.SpEntityId.ShouldBe(SpEntityId);
    }

    [Fact]
    public async Task ReturnsErrorWhenRelayStateExceedsMaxLength()
    {
        var samlOptions = new SamlOptions { MaxRelayStateLength = 80 };
        var svc = CreateService(samlOptions: samlOptions);
        var longRelayState = new string('x', 81);

        var result = await svc.CreateResponseAsync(
            CreateAuthenticatedContext(), SpEntityId, longRelayState, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("RelayState exceeds maximum length");
    }

    [Fact]
    public async Task ReturnsErrorWhenSpHasNoAcsUrls()
    {
        var sp = CreateSp(acsUrl: null);
        var svc = CreateService(spStore: new InMemorySamlServiceProviderStore([sp]));

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("no assertion consumer service URLs");
        result.Error!.ShouldNotContain(SpEntityId);
    }

    [Fact]
    public async Task ReturnsErrorWhenAcsUrlIsInvalid()
    {
        var sp = CreateSp(acsUrl: "not-a-valid-url");
        var svc = CreateService(spStore: new InMemorySamlServiceProviderStore([sp]));

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("invalid assertion consumer service URL");
        result.Error!.ShouldNotContain(SpEntityId);
    }

    [Fact]
    public async Task ReturnsErrorWhenUserNotAuthenticated()
    {
        var unauthenticatedSession = new MockUserSession
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()), // not authenticated
            SessionId = "test-session"
        };
        var svc = CreateService(userSession: unauthenticatedSession);

        var result = await svc.CreateResponseAsync(new DefaultHttpContext(), SpEntityId, null, _ct);

        result.IsError.ShouldBeTrue();
        result.Error!.ShouldContain("not authenticated");
    }

    [Fact]
    public async Task ReturnsSuccessForValidRequest()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        result.IsError.ShouldBeFalse();
        result.Response.ShouldNotBeNull();
        result.SpEntityId.ShouldBe(SpEntityId);
    }

    [Fact]
    public async Task SuccessResultContainsSaml2FrontChannelResult()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        var frontChannel = ExtractFrontChannelResult(result);
        frontChannel.Message.ShouldNotBeNull();
        frontChannel.Message.Name.ShouldBe("SAMLResponse");
    }

    [Fact]
    public async Task SuccessResultMessageTargetsAcsUrl()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        var frontChannel = ExtractFrontChannelResult(result);
        frontChannel.Message!.Destination.ShouldBe(SpAcsUrl);
    }

    [Fact]
    public async Task SuccessResultIncludesRelayStateWhenProvided()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(
            CreateAuthenticatedContext(), SpEntityId, "https://sp.example.com/deep", _ct);

        var frontChannel = ExtractFrontChannelResult(result);
        frontChannel.Message!.RelayState.ShouldBe("https://sp.example.com/deep");
    }

    [Fact]
    public async Task SuccessResultOmitsRelayStateWhenNotProvided()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        var frontChannel = ExtractFrontChannelResult(result);
        frontChannel.Message!.RelayState.ShouldBeNull();
    }

    [Fact]
    public async Task RecordsSamlSpSessionOnSuccess()
    {
        var userSession = new MockUserSession
        {
            User = CreateAuthenticatedUser(),
            SessionId = "test-session"
        };
        var svc = CreateService(userSession: userSession);

        await svc.CreateResponseAsync(CreateAuthenticatedContext(userSession.User), SpEntityId, null, _ct);

        userSession.SamlSessions.ShouldContain(s => s.EntityId == SpEntityId);
    }

    [Fact]
    public async Task ReusesExistingSessionIndexWhenSpSessionExists()
    {
        var existingSessionIndex = "existing-session-index";
        var userSession = new MockUserSession
        {
            User = CreateAuthenticatedUser(),
            SessionId = "test-session"
        };
        userSession.SamlSessions.Add(new Duende.IdentityServer.Saml.Models.SamlSpSessionData
        {
            EntityId = SpEntityId,
            SessionIndex = existingSessionIndex,
            NameId = "user-123"
        });
        var svc = CreateService(userSession: userSession);

        await svc.CreateResponseAsync(CreateAuthenticatedContext(userSession.User), SpEntityId, null, _ct);

        var session = userSession.SamlSessions.Single(s => s.EntityId == SpEntityId);
        session.SessionIndex.ShouldBe(existingSessionIndex);
    }

    [Fact]
    public async Task SetsIsIdpInitiatedOnValidatedRequest()
    {
        var responseGen = new StubResponseGenerator();
        var svc = CreateService(responseGenerator: responseGen);

        await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, null, _ct);

        responseGen.CapturedRequest.ShouldNotBeNull();
        responseGen.CapturedRequest.IsIdpInitiated.ShouldBeTrue();
    }

    [Fact]
    public async Task OverloadWithoutRelayStateSucceeds()
    {
        var svc = CreateService();

        var result = await svc.CreateResponseAsync(CreateAuthenticatedContext(), SpEntityId, _ct);

        result.IsError.ShouldBeFalse();
        var frontChannel = ExtractFrontChannelResult(result);
        frontChannel.Message!.RelayState.ShouldBeNull();
    }

    private static DefaultIdpInitiatedSsoService CreateService(
        ISamlServiceProviderStore? spStore = null,
        MockUserSession? userSession = null,
        StubResponseGenerator? responseGenerator = null,
        IdentityServerOptions? idServerOptions = null,
        SamlOptions? samlOptions = null)
    {
        spStore ??= new InMemorySamlServiceProviderStore([CreateSp()]);
        userSession ??= new MockUserSession
        {
            User = CreateAuthenticatedUser(),
            SessionId = "test-session"
        };
        responseGenerator ??= new StubResponseGenerator();
        samlOptions ??= new SamlOptions();
        idServerOptions ??= new IdentityServerOptions { Saml = samlOptions };

        return new DefaultIdpInitiatedSsoService(
            spStore,
            new DefaultSamlResourceResolver(
                new InMemoryResourcesStore([new IdentityResource("openid", ["sub"])], null, null),
                NullLogger<DefaultSamlResourceResolver>.Instance),
            userSession,
            responseGenerator,
            new StubSaml2IssuerNameService(IdpEntityId),
            Options.Create(idServerOptions),
            NullLogger<DefaultIdpInitiatedSsoService>.Instance);
    }

    private static SamlServiceProvider CreateSp(
        bool enabled = true,
        bool allowIdpInitiated = true,
        string? acsUrl = SpAcsUrl)
    {
        var sp = new SamlServiceProvider
        {
            EntityId = SpEntityId,
            Enabled = enabled,
            AllowIdpInitiated = allowIdpInitiated,
            AllowedScopes = new HashSet<string> { "openid" },
        };

        if (acsUrl != null)
        {
            sp.AssertionConsumerServiceUrls = [new IndexedEndpoint
            {
                Location = acsUrl,
                Binding = SamlBinding.HttpPost,
                Index = 0,
                IsDefault = true
            }];
        }
        else
        {
            sp.AssertionConsumerServiceUrls = [];
        }

        return sp;
    }

    private static ClaimsPrincipal CreateAuthenticatedUser() =>
        new(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-123")],
            "TestAuth"));

    private static DefaultHttpContext CreateAuthenticatedContext(ClaimsPrincipal? user = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = user ?? CreateAuthenticatedUser();
        return ctx;
    }

    private static Saml2FrontChannelResult ExtractFrontChannelResult(IdpInitiatedSsoResult result)
    {
        result.Response.ShouldNotBeNull();
        var autoPost = result.Response.ShouldBeOfType<SamlAutoPostResult>();
        return autoPost.FrontChannelResult;
    }

    internal sealed class StubResponseGenerator : ISaml2SsoResponseGenerator
    {
        public ValidatedAuthnRequest? CapturedRequest { get; private set; }

        public Task<Saml2FrontChannelResult> CreateResponse(ValidatedAuthnRequest validatedAuthnRequest, Ct ct)
        {
            CapturedRequest = validatedAuthnRequest;

            var doc = new XmlDocument();
            doc.LoadXml("<SAMLResponse/>");

            return Task.FromResult(new Saml2FrontChannelResult
            {
                Message = new OutboundSaml2Message
                {
                    Name = "SAMLResponse",
                    Xml = doc.DocumentElement!,
                    Destination = SpAcsUrl,
                    RelayState = validatedAuthnRequest.RelayState,
                    Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
                }
            });
        }

        public Task<Saml2FrontChannelResult> CreateErrorResponse(ValidatedAuthnRequest validatedAuthnRequest, Saml2InteractionResponse interactionResponse, Ct ct) =>
            Task.FromResult(new Saml2FrontChannelResult { Error = interactionResponse.Message });
    }

    private sealed class StubSpStore(SamlServiceProvider? sp) : ISamlServiceProviderStore
    {
        public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct) =>
            Task.FromResult(sp);

        public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] Ct ct)
        {
            if (sp != null)
            {
                yield return sp;
            }

            await Task.CompletedTask;
        }
    }
}
