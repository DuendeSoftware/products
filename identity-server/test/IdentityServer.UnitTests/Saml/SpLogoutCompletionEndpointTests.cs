// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting.FederatedSignOut;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class SpLogoutCompletionEndpointTests
{
    private const string Category = "SpLogoutCompletionEndpoint";
    private const string IdpEntityId = "https://idp.example.com";
    private const string ResponseDestination = "https://upstream-idp.example.com/slo";

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _timeProvider = new();

    private SpLogoutCompletionEndpoint CreateEndpoint(
        IMessageStore<SamlSpLogoutMessage>? messageStore = null,
        ISaml2SloResponseGenerator? responseGenerator = null,
        ISaml2IssuerNameService? issuerNameService = null,
        ISamlLogoutSessionStore? logoutSessionStore = null)
    {
        messageStore ??= new MockMessageStore<SamlSpLogoutMessage>();
        responseGenerator ??= new StubSloResponseGenerator();
        issuerNameService ??= new StubIssuerNameService("https://local.example.com");
        logoutSessionStore ??= new InMemorySamlLogoutSessionStore(_timeProvider, NullLogger<InMemorySamlLogoutSessionStore>.Instance);

        return new SpLogoutCompletionEndpoint(
            messageStore,
            responseGenerator,
            issuerNameService,
            logoutSessionStore,
            _timeProvider,
            NullLogger<SpLogoutCompletionEndpoint>.Instance);
    }

    private static DefaultHttpContext CreateGetContext(string? logoutId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        if (logoutId != null)
        {
            context.Request.QueryString = new QueryString($"?logoutId={logoutId}");
        }

        return context;
    }

    private MockMessageStore<SamlSpLogoutMessage> CreateMessageStoreWithMessage(
        SamlSpLogoutMessage data, DateTime? created = null)
    {
        var store = new MockMessageStore<SamlSpLogoutMessage>();
        store.Messages["test-logout-id"] = new Message<SamlSpLogoutMessage>(
            data, (created ?? _timeProvider.GetUtcNow().UtcDateTime));
        return store;
    }

    private static SamlSpLogoutMessage CreateValidLogoutMessage() => new()
    {
        IdpEntityId = IdpEntityId,
        LogoutRequestId = "_req-123",
        ResponseBinding = SamlConstants.Bindings.HttpPost,
        ResponseDestination = ResponseDestination,
        RelayState = "some-relay-state"
    };

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsMethodNotAllowedForNonGetRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        var endpoint = CreateEndpoint();

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsBadRequestWhenLogoutIdMissing()
    {
        var context = CreateGetContext(logoutId: null);
        var endpoint = CreateEndpoint();

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsBadRequestWhenMessageNotFound()
    {
        var context = CreateGetContext(logoutId: "nonexistent-id");
        var endpoint = CreateEndpoint(messageStore: new MockMessageStore<SamlSpLogoutMessage>());

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsBadRequestWhenMessageExpired()
    {
        var store = CreateMessageStoreWithMessage(
            CreateValidLogoutMessage(),
            created: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-6));
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsBadRequestWhenSamlIdpEntityIdMissing()
    {
        var message = CreateValidLogoutMessage() with { IdpEntityId = null! };
        var store = CreateMessageStoreWithMessage(message);
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsBadRequestWhenSamlLogoutRequestIdMissing()
    {
        var message = CreateValidLogoutMessage() with { LogoutRequestId = null! };
        var store = CreateMessageStoreWithMessage(message);
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsBadRequestWhenSamlResponseBindingMissing()
    {
        var message = CreateValidLogoutMessage() with { ResponseBinding = null! };
        var store = CreateMessageStoreWithMessage(message);
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsBadRequestWhenSamlResponseDestinationMissing()
    {
        var message = CreateValidLogoutMessage() with { ResponseDestination = null! };
        var store = CreateMessageStoreWithMessage(message);
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsPostResultForHttpPostBinding()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(_timeProvider, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["req-sp2"] = new("https://sp2.example.com"),
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("req-sp2", "https://sp2.example.com", true, _ct);

        var store = CreateMessageStoreWithMessage(CreateValidLogoutMessage());
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: store,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        var result = await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreateSuccessResponse));
        result.ShouldBeOfType<Saml2FrontChannelResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsRedirectResultForHttpRedirectBinding()
    {
        var message = CreateValidLogoutMessage() with { ResponseBinding = SamlConstants.Bindings.HttpRedirect };

        var sessionStore = new InMemorySamlLogoutSessionStore(_timeProvider, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["req-sp2"] = new("https://sp2.example.com"),
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("req-sp2", "https://sp2.example.com", true, _ct);

        var store = CreateMessageStoreWithMessage(message);
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: store,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        var result = await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreateSuccessResponse));
        result.ShouldBeOfType<Saml2FrontChannelResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CallsPartialLogoutWhenSomeSpsFailed()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(_timeProvider, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["req-sp2"] = new("https://sp2.example.com"),
                ["req-sp3"] = new("https://sp3.example.com")
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("req-sp2", "https://sp2.example.com", true, _ct);
        await sessionStore.TryRecordResponseAsync("req-sp3", "https://sp3.example.com", false, _ct);

        var store = CreateMessageStoreWithMessage(CreateValidLogoutMessage());
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: store,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        var result = await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreatePartialLogoutResponse));
        result.ShouldBeOfType<Saml2FrontChannelResult>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RemovesLogoutSessionAfterProcessing()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(_timeProvider, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["req-sp2"] = new("https://sp2.example.com")
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("req-sp2", "https://sp2.example.com", true, _ct);

        var store = CreateMessageStoreWithMessage(CreateValidLogoutMessage());
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: store,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        var remaining = await sessionStore.GetByLogoutIdAsync("test-logout-id", _ct);
        remaining.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CallsSuccessResponseWhenNoLogoutSessionExists()
    {
        var store = CreateMessageStoreWithMessage(CreateValidLogoutMessage());
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: store,
            responseGenerator: responseGenerator);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreateSuccessResponse));
    }

    private sealed class StubIssuerNameService(string entityId) : ISaml2IssuerNameService
    {
        public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(entityId);
    }

    private sealed class StubSloResponseGenerator : ISaml2SloResponseGenerator
    {
        public string? LastCalledMethod { get; private set; }

        public Task<Saml2FrontChannelResult> CreateSuccessResponse(ValidatedLogoutRequest request, Ct ct)
        {
            LastCalledMethod = nameof(CreateSuccessResponse);
            return Task.FromResult(new Saml2FrontChannelResult
            {
                Message = new OutboundSaml2Message
                {
                    Name = "SAMLResponse",
                    Xml = new XmlDocument().CreateElement("SAMLResponse"),
                    Destination = request.Saml2Sp?.SingleLogoutServiceUrls.FirstOrDefault()?.Location ?? string.Empty,
                    Binding = request.Binding,
                    RelayState = request.RelayState
                }
            });
        }

        public Task<Saml2FrontChannelResult> CreatePartialLogoutResponse(ValidatedLogoutRequest request, Ct ct)
        {
            LastCalledMethod = nameof(CreatePartialLogoutResponse);
            return Task.FromResult(new Saml2FrontChannelResult
            {
                Message = new OutboundSaml2Message
                {
                    Name = "SAMLResponse",
                    Xml = new XmlDocument().CreateElement("SAMLResponse"),
                    Destination = request.Saml2Sp?.SingleLogoutServiceUrls.FirstOrDefault()?.Location ?? string.Empty,
                    Binding = request.Binding,
                    RelayState = request.RelayState
                }
            });
        }
    }
}
