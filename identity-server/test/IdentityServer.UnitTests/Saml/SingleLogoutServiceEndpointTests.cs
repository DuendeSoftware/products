// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Licensing;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class SingleLogoutServiceEndpointTests
{
    private const string Category = "SingleLogoutServiceEndpoint";
    private const string SpEntityId = "https://sp.example.com";
    private const string SpSloUrl = "https://sp.example.com/slo";
    private const string IdpEntityId = "https://idp.example.com";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static SamlServiceProvider CreateSp() => new()
    {
        EntityId = SpEntityId,
        Enabled = true,
        SingleLogoutServiceUrls = [new SamlEndpointType { Location = SpSloUrl, Binding = SamlBinding.HttpRedirect }]
    };

    private static SingleLogoutServiceEndpoint CreateEndpoint(
        IFrontChannelBinding? binding = null,
        MockUserSession? userSession = null,
        ILogoutRequestValidator? validator = null,
        ISaml2SloResponseGenerator? responseGenerator = null,
        IdentityServerOptions? options = null,
        SamlServiceProvider? sp = null,
        ISamlLogoutSessionStore? sessionStore = null)
    {
        var bindings = binding != null
            ? new[] { binding }
            : Array.Empty<IFrontChannelBinding>();

        userSession ??= new MockUserSession();
        validator ??= new AlwaysSuccessValidator();
        responseGenerator ??= new StubSloResponseGenerator();
        options ??= TestIdentityServerOptions.Create();
        sessionStore ??= new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);

        sp ??= CreateSp();
        var spStore = new InMemorySamlServiceProviderStore([sp]);
        var serviceProviderEntityResolver = new ServiceProviderEntityResolver(spStore);

        return new SingleLogoutServiceEndpoint(
            bindings,
            serviceProviderEntityResolver,
            new SamlXmlReader(),
            userSession,
            validator,
            new StubSloIssuerNameService(IdpEntityId),
            responseGenerator,
            sessionStore,
            new TestEventService(),
            options,
            spStore,
            IdentityServerLicenseValidator.CreateForTests(),
            NullLogger<SingleLogoutServiceEndpoint>.Instance);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorForUnsupportedMethod()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Delete;
        var endpoint = CreateEndpoint();

        var result = await endpoint.ProcessAsync(context);

        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldBe("Method not allowed");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorWhenNoBindingMatches()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        // No bindings registered
        var endpoint = CreateEndpoint(binding: null);

        var result = await endpoint.ProcessAsync(context);

        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldBe("No front channel binding found to satisfy request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorWhenBindingThrowsFormatException()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var endpoint = CreateEndpoint(binding: new ThrowingBinding(new FormatException("bad base64")));

        var result = await endpoint.ProcessAsync(context);

        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldBe("Invalid base64 encoding in SAML logout message");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorWhenXmlParsingFails()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var endpoint = CreateEndpoint(binding: new StubBinding(CreateInvalidXmlMessage()));

        var result = await endpoint.ProcessAsync(context);

        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldBe("The SAML logout request could not be processed");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorWhenValidationFails()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var endpoint = CreateEndpoint(
            binding: new StubBinding(CreateValidLogoutRequestMessage()),
            validator: new AlwaysFailValidator("Validation failed"));

        var result = await endpoint.ProcessAsync(context);

        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldBe("Validation failed");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsSuccessResponseWhenNoUserAuthenticated()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var userSession = new MockUserSession { User = null };
        var endpoint = CreateEndpoint(
            binding: new StubBinding(CreateValidLogoutRequestMessage()),
            userSession: userSession);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2FrontChannelResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsLogoutPageResultWhenUserAuthenticated()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var userSession = new MockUserSession
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"))
        };
        var endpoint = CreateEndpoint(
            binding: new StubBinding(CreateValidLogoutRequestMessage()),
            userSession: userSession);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2LogoutPageResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_ValidLogoutResponse_ReturnsOk()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var endpoint = CreateEndpoint(binding: new StubBinding(CreateValidLogoutResponseMessage()));

        var result = await endpoint.ProcessAsync(context);

        var statusResult = result.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_MalformedXml_ReturnsOk()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var endpoint = CreateEndpoint(binding: new StubBinding(CreateInvalidLogoutResponseMessage()));

        var result = await endpoint.ProcessAsync(context);

        var statusResult = result.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_NonSuccessStatus_ReturnsOk()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var endpoint = CreateEndpoint(binding: new StubBinding(CreateNonSuccessLogoutResponseMessage()));

        var result = await endpoint.ProcessAsync(context);

        var statusResult = result.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_UnexpectedMessageName_ReturnsError()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var endpoint = CreateEndpoint(binding: new StubBinding(CreateUnknownNameMessage()));

        var result = await endpoint.ProcessAsync(context);

        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldBe("Unexpected SAML message type: UnknownType");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_UnsignedResponse_RejectedWhenSignatureRequired()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var sessionStore = new SpySessionStore();
        // SP defaults to RequireSignedLogoutResponses = null (which means required)
        var endpoint = CreateEndpoint(
            binding: new StubBinding(CreateUnsignedLogoutResponseMessage()),
            sessionStore: sessionStore);

        var result = await endpoint.ProcessAsync(context);

        // Should still return OK (we don't expose errors to the SP for responses)
        var statusResult = result.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
        // The response should NOT have been recorded
        sessionStore.RecordedResponses.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_UnsignedResponse_AcceptedWhenSignatureNotRequired()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var sp = CreateSp();
        sp.RequireSignedLogoutResponses = false;
        var sessionStore = new SpySessionStore();
        var endpoint = CreateEndpoint(
            binding: new StubBinding(CreateUnsignedLogoutResponseMessage()),
            sp: sp,
            sessionStore: sessionStore);

        var result = await endpoint.ProcessAsync(context);

        var statusResult = result.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
        sessionStore.RecordedResponses.Count.ShouldBe(1);
        sessionStore.RecordedResponses[0].Success.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_SignedResponse_AcceptedWhenSignatureRequired()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        // SP requires signature (default), message has TrustLevel.ConfiguredKey
        var sessionStore = new SpySessionStore();
        var endpoint = CreateEndpoint(
            binding: new StubBinding(CreateValidLogoutResponseMessage()),
            sessionStore: sessionStore);

        var result = await endpoint.ProcessAsync(context);

        var statusResult = result.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
        sessionStore.RecordedResponses.Count.ShouldBe(1);
        sessionStore.RecordedResponses[0].Success.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SAMLResponse_PartialLogoutSubStatus_TreatedAsNonSuccess()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        var sessionStore = new SpySessionStore();
        var sp = CreateSp();
        sp.RequireSignedLogoutResponses = false;
        var endpoint = CreateEndpoint(
            binding: new StubBinding(CreatePartialLogoutResponseMessage()),
            sp: sp,
            sessionStore: sessionStore);

        var result = await endpoint.ProcessAsync(context);

        var statusResult = result.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
        // PartialLogout sub-status should be recorded as non-success
        sessionStore.RecordedResponses.ShouldHaveSingleItem();
        sessionStore.RecordedResponses[0].Success.ShouldBeFalse();
    }

    private static InboundSaml2Message CreateValidLogoutResponseMessage()
    {
        var xml = """
            <samlp:LogoutResponse xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                  xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                  ID="_response-id-123"
                                  Version="2.0"
                                  IssueInstant="2025-06-15T12:00:00Z"
                                  InResponseTo="_original-request-id">
                <saml:Issuer>https://sp.example.com</saml:Issuer>
                <samlp:Status>
                    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success"/>
                </samlp:Status>
            </samlp:LogoutResponse>
            """;
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        return new InboundSaml2Message
        {
            Name = "SAMLResponse",
            Xml = xmlDoc.DocumentElement!,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.ConfiguredKey
        };
    }

    private static InboundSaml2Message CreateInvalidLogoutResponseMessage()
    {
        var xmlDoc = new XmlDocument();
        var element = xmlDoc.CreateElement("NotALogoutResponse");
        return new InboundSaml2Message
        {
            Name = "SAMLResponse",
            Xml = element,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.None
        };
    }

    private static InboundSaml2Message CreateNonSuccessLogoutResponseMessage()
    {
        var xml = """
            <samlp:LogoutResponse xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                  xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                  ID="_response-id-456"
                                  Version="2.0"
                                  IssueInstant="2025-06-15T12:00:00Z"
                                  InResponseTo="_original-request-id">
                <saml:Issuer>https://sp.example.com</saml:Issuer>
                <samlp:Status>
                    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Requester"/>
                </samlp:Status>
            </samlp:LogoutResponse>
            """;
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        return new InboundSaml2Message
        {
            Name = "SAMLResponse",
            Xml = xmlDoc.DocumentElement!,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.ConfiguredKey
        };
    }

    private static InboundSaml2Message CreateUnknownNameMessage()
    {
        var xmlDoc = new XmlDocument();
        var element = xmlDoc.CreateElement("Something");
        return new InboundSaml2Message
        {
            Name = "UnknownType",
            Xml = element,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.None
        };
    }

    private static InboundSaml2Message CreateValidLogoutRequestMessage()
    {
        var xml = """
            <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                 xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                 ID="_test-id-123"
                                 Version="2.0"
                                 IssueInstant="2025-06-15T12:00:00Z"
                                 Destination="https://idp.example.com/Saml2/SLO">
                <saml:Issuer>https://sp.example.com</saml:Issuer>
                <saml:NameID>user@example.com</saml:NameID>
            </samlp:LogoutRequest>
            """;
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        return new InboundSaml2Message
        {
            Name = "SAMLRequest",
            Xml = xmlDoc.DocumentElement!,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.ConfiguredKey
        };
    }

    private static InboundSaml2Message CreateInvalidXmlMessage()
    {
        var xmlDoc = new XmlDocument();
        var element = xmlDoc.CreateElement("NotALogoutRequest");
        return new InboundSaml2Message
        {
            Name = "SAMLRequest",
            Xml = element,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.None
        };
    }

    private sealed class StubBinding(InboundSaml2Message message) : IFrontChannelBinding
    {
        public string Identifier => SamlConstants.Bindings.HttpRedirect;

        public bool CanUnBind(HttpRequest httpRequest) => true;

        public Task<InboundSaml2Message> UnBindAsync(HttpRequest httpRequest,
            Func<string, Ct, Task<Duende.IdentityServer.Saml.Saml2Entity?>> entityResolver)
            => Task.FromResult(message);

        public Task BindAsync(HttpResponse httpResponse, OutboundSaml2Message message) => Task.CompletedTask;
    }

    private sealed class ThrowingBinding(Exception exception) : IFrontChannelBinding
    {
        public string Identifier => SamlConstants.Bindings.HttpRedirect;

        public bool CanUnBind(HttpRequest httpRequest) => true;

        public Task<InboundSaml2Message> UnBindAsync(HttpRequest httpRequest,
            Func<string, Ct, Task<Duende.IdentityServer.Saml.Saml2Entity?>> entityResolver)
            => throw exception;

        public Task BindAsync(HttpResponse httpResponse, OutboundSaml2Message message) => Task.CompletedTask;
    }

    private sealed class AlwaysSuccessValidator : ILogoutRequestValidator
    {
        public Task<LogoutRequestValidationResult> ValidateAsync(ValidatedLogoutRequest request, Ct ct)
        {
            request.Saml2Sp = new SamlServiceProvider
            {
                EntityId = SpEntityId,
                Enabled = true,
                SingleLogoutServiceUrls = [new SamlEndpointType { Location = SpSloUrl, Binding = SamlBinding.HttpRedirect }]
            };
            return Task.FromResult(LogoutRequestValidationResult.Valid(request));
        }
    }

    private sealed class AlwaysFailValidator(string error) : ILogoutRequestValidator
    {
        public Task<LogoutRequestValidationResult> ValidateAsync(ValidatedLogoutRequest request, Ct ct)
            => Task.FromResult(LogoutRequestValidationResult.InValid(request, error, error));
    }

    private sealed class StubSloResponseGenerator : ISaml2SloResponseGenerator
    {
        public Task<Saml2FrontChannelResult> CreateSuccessResponse(ValidatedLogoutRequest request, Ct ct)
            => Task.FromResult(new Saml2FrontChannelResult
            {
                Message = new OutboundSaml2Message
                {
                    Name = "SAMLResponse",
                    Xml = new XmlDocument().CreateElement("SAMLResponse"),
                    Destination = SpSloUrl,
                    Binding = SamlConstants.Bindings.HttpPost
                }
            });

        public Task<Saml2FrontChannelResult> CreatePartialLogoutResponse(ValidatedLogoutRequest request, Ct ct)
            => Task.FromResult(new Saml2FrontChannelResult
            {
                Message = new OutboundSaml2Message
                {
                    Name = "SAMLResponse",
                    Xml = new XmlDocument().CreateElement("SAMLResponse"),
                    Destination = SpSloUrl,
                    Binding = SamlConstants.Bindings.HttpPost
                }
            });

        public Task<Saml2FrontChannelResult> CreateErrorResponse(ValidatedLogoutRequest request, string errorStatusCode, string? subStatusCode, string? statusMessage, Ct ct)
            => Task.FromResult(new Saml2FrontChannelResult { Error = errorStatusCode });
    }

    private sealed class StubSloIssuerNameService(string entityId) : ISaml2IssuerNameService
    {
        public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(entityId);
    }

    private static InboundSaml2Message CreateUnsignedLogoutResponseMessage()
    {
        var xml = """
            <samlp:LogoutResponse xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                  xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                  ID="_response-unsigned"
                                  Version="2.0"
                                  IssueInstant="2025-06-15T12:00:00Z"
                                  InResponseTo="_original-request-id">
                <saml:Issuer>https://sp.example.com</saml:Issuer>
                <samlp:Status>
                    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success"/>
                </samlp:Status>
            </samlp:LogoutResponse>
            """;
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        return new InboundSaml2Message
        {
            Name = "SAMLResponse",
            Xml = xmlDoc.DocumentElement!,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.None
        };
    }

    private static InboundSaml2Message CreatePartialLogoutResponseMessage()
    {
        var xml = """
            <samlp:LogoutResponse xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                  xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                  ID="_response-partial"
                                  Version="2.0"
                                  IssueInstant="2025-06-15T12:00:00Z"
                                  InResponseTo="_original-request-id">
                <saml:Issuer>https://sp.example.com</saml:Issuer>
                <samlp:Status>
                    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success">
                        <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:PartialLogout"/>
                    </samlp:StatusCode>
                </samlp:Status>
            </samlp:LogoutResponse>
            """;
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        return new InboundSaml2Message
        {
            Name = "SAMLResponse",
            Xml = xmlDoc.DocumentElement!,
            Destination = "https://idp.example.com/Saml2/SLO",
            Binding = SamlConstants.Bindings.HttpRedirect,
            TrustLevel = TrustLevel.ConfiguredKey
        };
    }

    private sealed class SpySessionStore : ISamlLogoutSessionStore
    {
        public List<(string RequestId, string Issuer, bool Success)> RecordedResponses { get; } = [];

        public Task StoreAsync(SamlLogoutSession session, Ct ct) => Task.CompletedTask;

        public Task<SamlLogoutSession?> GetByLogoutIdAsync(string logoutId, Ct ct) =>
            Task.FromResult<SamlLogoutSession?>(null);

        public Task<bool> TryRecordResponseAsync(string requestId, string issuer, bool success, Ct ct)
        {
            RecordedResponses.Add((requestId, issuer, success));
            return Task.FromResult(true);
        }

        public Task RemoveAsync(string logoutId, Ct ct) => Task.CompletedTask;
    }
}
