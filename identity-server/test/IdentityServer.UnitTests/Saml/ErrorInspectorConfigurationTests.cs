// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Configuration;
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
using Duende.IdentityServer.Saml.Xml;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class ErrorInspectorConfigurationTests
{
    private const string SpEntityId = "https://sp.example.com";
    private const string IdpEntityId = "https://idp.example.com";
    private const string SpSloUrl = "https://sp.example.com/slo";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AuthnRequestErrorInspectorCanSuppressErrors()
    {
        var reader = new SamlXmlReader();
        // Create an AuthnRequest with an unprocessed child element to trigger an error
        var xml = CreateAuthnRequestWithUnprocessedChild();
        var traverser = new XmlTraverser(xml);

        // Without inspector, should throw
        await Should.ThrowAsync<SamlXmlException>(
            () => reader.ReadAuthnRequestAsync(traverser, null, _ct));

        // With inspector that suppresses errors, should succeed
        traverser = new XmlTraverser(xml);
        var inspectorCalled = false;
        var result = await reader.ReadAuthnRequestAsync(traverser, context =>
        {
            inspectorCalled = true;
            foreach (var error in context.Errors)
            {
                error.Ignore = true;
            }
        }, _ct);

        inspectorCalled.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Issuer?.Value.ShouldBe(SpEntityId);
    }

    [Fact]
    public async Task LogoutRequestErrorInspectorCanSuppressErrors()
    {
        var reader = new SamlXmlReader();
        var xml = CreateLogoutRequestWithUnprocessedChild();
        var traverser = new XmlTraverser(xml);

        await Should.ThrowAsync<SamlXmlException>(
            () => reader.ReadLogoutRequestAsync(traverser, null, _ct));

        traverser = new XmlTraverser(xml);
        var inspectorCalled = false;
        var result = await reader.ReadLogoutRequestAsync(traverser, context =>
        {
            inspectorCalled = true;
            foreach (var error in context.Errors)
            {
                error.Ignore = true;
            }
        }, _ct);

        inspectorCalled.ShouldBeTrue();
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task LogoutResponseErrorInspectorCanSuppressErrors()
    {
        var reader = new SamlXmlReader();
        var xml = CreateLogoutResponseWithUnprocessedChild();
        var traverser = new XmlTraverser(xml);

        await Should.ThrowAsync<SamlXmlException>(
            () => reader.ReadLogoutResponseAsync(traverser, null, _ct));

        traverser = new XmlTraverser(xml);
        var inspectorCalled = false;
        var result = await reader.ReadLogoutResponseAsync(traverser, context =>
        {
            inspectorCalled = true;
            foreach (var error in context.Errors)
            {
                error.Ignore = true;
            }
        }, _ct);

        inspectorCalled.ShouldBeTrue();
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task SloEndpointPassesLogoutRequestErrorInspectorFromOptions()
    {
        var inspectorCalled = false;
        var options = TestIdentityServerOptions.Create();
        options.Saml.LogoutRequestErrorInspector = context =>
        {
            inspectorCalled = true;
            foreach (var error in context.Errors)
            {
                error.Ignore = true;
            }
        };

        var endpoint = CreateSloEndpoint(
            binding: new StubBinding(CreateSaml2MessageWithUnprocessedChild(
                "SAMLRequest", CreateLogoutRequestWithUnprocessedChild())),
            options: options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;

        var result = await endpoint.ProcessAsync(httpContext);

        inspectorCalled.ShouldBeTrue();
        // Should NOT be an error result since the inspector suppressed the parse error
        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldBeNull();
    }

    [Fact]
    public async Task SloEndpointPassesLogoutResponseErrorInspectorFromOptions()
    {
        var inspectorCalled = false;
        var options = TestIdentityServerOptions.Create();
        options.Saml.LogoutResponseErrorInspector = context =>
        {
            inspectorCalled = true;
            foreach (var error in context.Errors)
            {
                error.Ignore = true;
            }
        };

        var endpoint = CreateSloEndpoint(
            binding: new StubBinding(CreateSaml2MessageWithUnprocessedChild(
                "SAMLResponse", CreateLogoutResponseWithUnprocessedChild())),
            options: options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;

        var result = await endpoint.ProcessAsync(httpContext);

        inspectorCalled.ShouldBeTrue();
        // Should return OK since the inspector suppressed the parse error
        var statusResult = result.ShouldBeOfType<Duende.IdentityServer.Endpoints.Results.StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task SloEndpointDoesNotInvokeInspectorWhenNotConfigured()
    {
        // Default options — no inspector configured
        var options = TestIdentityServerOptions.Create();

        var endpoint = CreateSloEndpoint(
            binding: new StubBinding(CreateSaml2MessageWithUnprocessedChild(
                "SAMLRequest", CreateLogoutRequestWithUnprocessedChild())),
            options: options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;

        var result = await endpoint.ProcessAsync(httpContext);

        // Should return error since no inspector to suppress the parse error
        var frontChannelResult = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannelResult.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task SsoEndpointPassesAuthnRequestErrorInspectorFromOptions()
    {
        var inspectorCalled = false;
        var options = TestIdentityServerOptions.Create();
        options.UserInteraction.LoginUrl = "/account/login";
        options.UserInteraction.LoginReturnUrlParameter = "returnUrl";
        options.Saml.AuthnRequestErrorInspector = context =>
        {
            inspectorCalled = true;
            foreach (var error in context.Errors)
            {
                error.Ignore = true;
            }
        };

        var endpoint = CreateSsoEndpoint(
            binding: new StubBinding(CreateSaml2MessageWithUnprocessedChild(
                "SAMLRequest", CreateAuthnRequestWithUnprocessedChild())),
            options: options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;

        var result = await endpoint.ProcessAsync(httpContext);

        inspectorCalled.ShouldBeTrue();
        // Should NOT be a parse error — the inspector suppressed it, so the endpoint
        // proceeds to validation/interaction (returns login page since no user is authenticated)
        result.ShouldBeOfType<Saml2LoginPageResult>();
    }

    private static SingleLogoutServiceEndpoint CreateSloEndpoint(
        IFrontChannelBinding? binding = null,
        IdentityServerOptions? options = null)
    {
        var bindings = binding != null
            ? new[] { binding }
            : Array.Empty<IFrontChannelBinding>();

        options ??= TestIdentityServerOptions.Create();

        var sp = new SamlServiceProvider
        {
            EntityId = SpEntityId,
            Enabled = true,
            SingleLogoutServiceUrls = [new SamlEndpointType { Location = SpSloUrl, Binding = SamlBinding.HttpRedirect }]
        };
        var spStore = new InMemorySamlServiceProviderStore([sp]);
        var serviceProviderEntityResolver = new ServiceProviderEntityResolver(spStore);

        return new SingleLogoutServiceEndpoint(
            bindings,
            serviceProviderEntityResolver,
            new SamlXmlReader(),
            new MockUserSession(),
            new AlwaysSuccessValidator(),
            new StubIssuerNameService(IdpEntityId),
            new StubSloResponseGenerator(),
            new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance),
            new TestEventService(),
            options,
            spStore,
            NullLogger<SingleLogoutServiceEndpoint>.Instance);
    }

    private static SingleSignOnServiceEndpoint CreateSsoEndpoint(
        IFrontChannelBinding? binding = null,
        IdentityServerOptions? options = null)
    {
        var bindings = binding != null
            ? new[] { binding }
            : Array.Empty<IFrontChannelBinding>();

        options ??= TestIdentityServerOptions.Create();

        var sp = new SamlServiceProvider
        {
            EntityId = SpEntityId,
            Enabled = true,
            AssertionConsumerServiceUrls = { new IndexedEndpoint { Location = "https://sp.example.com/acs" } }
        };
        var spStore = new InMemorySamlServiceProviderStore([sp]);
        var serviceProviderEntityResolver = new ServiceProviderEntityResolver(spStore);

        return new SingleSignOnServiceEndpoint(
            bindings,
            serviceProviderEntityResolver,
            new SamlXmlReader(),
            new MockUserSession(),
            options,
            new StubAuthnRequestValidator(),
            new StubIssuerNameService(IdpEntityId),
            new StubSsoInteractionResponseGenerator(),
            new StubSsoResponseGenerator(),
            IdentityServerLicenseValidator.CreateForTests(),
            new TestEventService(),
            NullLogger<SingleSignOnServiceEndpoint>.Instance);
    }

    private static XmlElement CreateAuthnRequestWithUnprocessedChild()
    {
        var doc = new XmlDocument();
        var authnRequest = doc.CreateElement(
            "samlp", "AuthnRequest", SamlConstants.Namespaces.Protocol);
        authnRequest.SetAttribute("ID", "_test-id");
        authnRequest.SetAttribute("Version", "2.0");
        authnRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(authnRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = SpEntityId;
        authnRequest.AppendChild(issuer);

        // Add a Conditions element with an unprocessed child to trigger an error
        var conditions = doc.CreateElement(
            "saml", "Conditions", SamlConstants.Namespaces.Assertion);
        var proxyRestriction = doc.CreateElement(
            "saml", "ProxyRestriction", SamlConstants.Namespaces.Assertion);
        proxyRestriction.SetAttribute("Count", "2");
        conditions.AppendChild(proxyRestriction);
        authnRequest.AppendChild(conditions);

        return doc.DocumentElement!;
    }

    private static XmlElement CreateLogoutRequestWithUnprocessedChild()
    {
        var doc = new XmlDocument();
        var logoutRequest = doc.CreateElement(
            "samlp", "LogoutRequest", SamlConstants.Namespaces.Protocol);
        logoutRequest.SetAttribute("ID", "_test-id");
        logoutRequest.SetAttribute("Version", "2.0");
        logoutRequest.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(logoutRequest);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = SpEntityId;
        logoutRequest.AppendChild(issuer);

        var nameId = doc.CreateElement(
            "saml", "NameID", SamlConstants.Namespaces.Assertion);
        nameId.InnerText = "user@example.com";
        logoutRequest.AppendChild(nameId);

        // Add an unknown child element to trigger an unprocessed-child error
        var unknown = doc.CreateElement(
            "samlp", "Extensions", SamlConstants.Namespaces.Protocol);
        unknown.InnerText = "unexpected";
        logoutRequest.AppendChild(unknown);

        return doc.DocumentElement!;
    }

    private static XmlElement CreateLogoutResponseWithUnprocessedChild()
    {
        var doc = new XmlDocument();
        var logoutResponse = doc.CreateElement(
            "samlp", "LogoutResponse", SamlConstants.Namespaces.Protocol);
        logoutResponse.SetAttribute("ID", "_test-id");
        logoutResponse.SetAttribute("Version", "2.0");
        logoutResponse.SetAttribute("IssueInstant", "2025-01-01T00:00:00Z");
        doc.AppendChild(logoutResponse);

        var issuer = doc.CreateElement(
            "saml", "Issuer", SamlConstants.Namespaces.Assertion);
        issuer.InnerText = SpEntityId;
        logoutResponse.AppendChild(issuer);

        var status = doc.CreateElement(
            "samlp", "Status", SamlConstants.Namespaces.Protocol);
        var statusCode = doc.CreateElement(
            "samlp", "StatusCode", SamlConstants.Namespaces.Protocol);
        statusCode.SetAttribute("Value", "urn:oasis:names:tc:SAML:2.0:status:Success");
        status.AppendChild(statusCode);
        logoutResponse.AppendChild(status);

        // Add an unknown child element to trigger an unprocessed-child error
        var unknown = doc.CreateElement(
            "samlp", "Extensions", SamlConstants.Namespaces.Protocol);
        unknown.InnerText = "unexpected";
        logoutResponse.AppendChild(unknown);

        return doc.DocumentElement!;
    }

    private static InboundSaml2Message CreateSaml2MessageWithUnprocessedChild(string name, XmlElement xml) => new()
    {
        Name = name,
        Xml = xml,
        Destination = "https://idp.example.com/Saml2/SLO",
        Binding = SamlConstants.Bindings.HttpRedirect,
        TrustLevel = TrustLevel.ConfiguredKey
    };

    private sealed class StubBinding(InboundSaml2Message message) : IFrontChannelBinding
    {
        public string Identifier => SamlConstants.Bindings.HttpRedirect;
        public bool CanUnBind(HttpRequest httpRequest) => true;
        public Task<InboundSaml2Message> UnBindAsync(HttpRequest httpRequest,
            Func<string, Ct, Task<Duende.IdentityServer.Saml.Saml2Entity?>> entityResolver)
            => Task.FromResult(message);
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

        public Task<Saml2FrontChannelResult> CreateErrorResponse(ValidatedLogoutRequest request,
            string errorStatusCode, string? subStatusCode, string? statusMessage, Ct ct)
            => Task.FromResult(new Saml2FrontChannelResult { Error = errorStatusCode });

        public Task<Saml2FrontChannelResult> CreatePartialLogoutResponse(ValidatedLogoutRequest request, Ct ct)
            => Task.FromResult(new Saml2FrontChannelResult());
    }

    private sealed class StubIssuerNameService(string entityId) : ISaml2IssuerNameService
    {
        public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(entityId);
    }

    private sealed class StubAuthnRequestValidator : IAuthnRequestValidator
    {
        public Task<AuthnRequestValidationResult> ValidateAsync(ValidatedAuthnRequest request, Ct ct)
            => Task.FromResult(AuthnRequestValidationResult.Valid(request));
    }

    private sealed class StubSsoInteractionResponseGenerator : ISaml2SsoInteractionResponseGenerator
    {
        public Task<Saml2InteractionResponse> ProcessInteractionAsync(ValidatedAuthnRequest request, Ct ct)
            => Task.FromResult(Saml2InteractionResponse.Login());
    }

    private sealed class StubSsoResponseGenerator : ISaml2SsoResponseGenerator
    {
        public Task<Saml2FrontChannelResult> CreateResponse(ValidatedAuthnRequest validatedAuthnRequest, Ct ct)
            => Task.FromResult(new Saml2FrontChannelResult());

        public Task<Saml2FrontChannelResult> CreateErrorResponse(ValidatedAuthnRequest validatedAuthnRequest,
            Saml2InteractionResponse interactionResponse, Ct ct)
            => Task.FromResult(new Saml2FrontChannelResult { Error = "error" });
    }
}
