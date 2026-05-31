// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
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
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class SingleLogoutCallbackEndpointTests
{
    private const string Category = "SingleLogoutCallbackEndpoint";
    private const string SpEntityId = "https://sp.example.com";
    private const string SpSloUrl = "https://sp.example.com/slo";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static SamlServiceProvider CreateSp(bool enabled = true, bool hasSloUrl = true) => new()
    {
        EntityId = SpEntityId,
        Enabled = enabled,
        SingleLogoutServiceUrls = hasSloUrl ? [new SamlEndpointType { Location = SpSloUrl, Binding = SamlBinding.HttpRedirect }] : []
    };

    private static SingleLogoutCallbackEndpoint CreateEndpoint(
        IMessageStore<LogoutMessage>? messageStore = null,
        ISamlServiceProviderStore? spStore = null,
        ISaml2SloResponseGenerator? responseGenerator = null,
        ISaml2IssuerNameService? issuerNameService = null,
        ISamlLogoutSessionStore? logoutSessionStore = null)
    {
        messageStore ??= new MockMessageStore<LogoutMessage>();
        spStore ??= new InMemorySamlServiceProviderStore([CreateSp()]);
        responseGenerator ??= new StubSloResponseGenerator();
        issuerNameService ??= new StubIssuerNameService("https://idp.example.com");
        logoutSessionStore ??= new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);

        return new SingleLogoutCallbackEndpoint(
            new IdentityServerOptions(),
            messageStore,
            spStore,
            responseGenerator,
            issuerNameService,
            logoutSessionStore,
            new TestEventService(),
            IdentityServerLicenseValidator.CreateForTests(),
            NullLogger<SingleLogoutCallbackEndpoint>.Instance);
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

    private static MockMessageStore<LogoutMessage> CreateMessageStoreWithMessage(LogoutMessage data)
    {
        var store = new MockMessageStore<LogoutMessage>();
        store.Messages["test-logout-id"] = new Message<LogoutMessage>(data, DateTimeOffset.UtcNow.UtcDateTime);
        return store;
    }

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
    public async Task ReturnsErrorWhenLogoutIdMissing()
    {
        var context = CreateGetContext(logoutId: null);
        var endpoint = CreateEndpoint();

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2FrontChannelResult>()
            .Error.ShouldBe("Missing or invalid SAML logout state identifier");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorWhenLogoutMessageNotFound()
    {
        var context = CreateGetContext(logoutId: "nonexistent-id");
        var endpoint = CreateEndpoint(messageStore: new MockMessageStore<LogoutMessage>());

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2FrontChannelResult>()
            .Error.ShouldBe("SAML logout state not found or expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorWhenSpEntityIdMissing()
    {
        var store = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = null,
            SamlLogoutRequestId = "_req-id"
        });
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2FrontChannelResult>()
            .Error.ShouldBe("SAML logout state is missing service provider information");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsErrorWhenSpNotFound()
    {
        var store = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = "https://unknown.example.com",
            SamlLogoutRequestId = "_req-id"
        });
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2FrontChannelResult>()
            .Error.ShouldBe("SAML service provider not found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsNotFoundWhenSpDisabled()
    {
        var store = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: store,
            spStore: new InMemorySamlServiceProviderStore([CreateSp(enabled: false)]));

        var result = await endpoint.ProcessAsync(context);

        // InMemorySamlServiceProviderStore filters out disabled SPs at the store level,
        // so a disabled SP appears as "not found" to the endpoint.
        result.ShouldBeOfType<Saml2FrontChannelResult>()
            .Error.ShouldBe("SAML service provider not found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsSuccessResponseOnHappyPath()
    {
        var store = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(messageStore: store);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2FrontChannelResult>()
            .Message.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsSuccessWhenAllSpsRespondedSuccessfully()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
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
        await sessionStore.TryRecordResponseAsync("req-sp3", "https://sp3.example.com", true, _ct);

        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreateSuccessResponse));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsPartialLogoutWhenSpResponseMissing()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
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
        // Only sp2 responded — sp3 is still pending
        await sessionStore.TryRecordResponseAsync("req-sp2", "https://sp2.example.com", true, _ct);

        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreatePartialLogoutResponse));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsPartialLogoutWhenSpReportedFailure()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
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

        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreatePartialLogoutResponse));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsPartialLogoutWhenNoLogoutSessionExists()
    {
        // No session in the store — should return partial (we don't know the outcome)
        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            responseGenerator: responseGenerator);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreatePartialLogoutResponse));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsSuccessWhenLogoutSessionHasNoExpectedResponses()
    {
        // Empty ExpectedResponses means no SPs to notify (initiating SP was the only one)
        var sessionStore = new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>(),
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);

        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreateSuccessResponse));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RemovesLogoutSessionAfterProcessing()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
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

        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        // Session should be cleaned up
        var remaining = await sessionStore.GetByLogoutIdAsync("test-logout-id", _ct);
        remaining.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsPartialLogoutWhenSkippedSpCountGreaterThanZero()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>(),
            SkippedSpCount = 2,
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);

        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreatePartialLogoutResponse));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsPartialLogoutWhenAllResponsesSucceededButSkippedSpCountGreaterThanZero()
    {
        var sessionStore = new InMemorySamlLogoutSessionStore(TimeProvider.System, NullLogger<InMemorySamlLogoutSessionStore>.Instance);
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout-id",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["req-sp2"] = new("https://sp2.example.com")
            },
            SkippedSpCount = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("req-sp2", "https://sp2.example.com", true, _ct);

        var messageStore = CreateMessageStoreWithMessage(new LogoutMessage
        {
            SamlServiceProviderEntityId = SpEntityId,
            SamlLogoutRequestId = "_req-id"
        });
        var responseGenerator = new StubSloResponseGenerator();
        var context = CreateGetContext(logoutId: "test-logout-id");
        var endpoint = CreateEndpoint(
            messageStore: messageStore,
            responseGenerator: responseGenerator,
            logoutSessionStore: sessionStore);

        await endpoint.ProcessAsync(context);

        responseGenerator.LastCalledMethod.ShouldBe(nameof(StubSloResponseGenerator.CreatePartialLogoutResponse));
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
                    Destination = SpSloUrl,
                    Binding = SamlConstants.Bindings.HttpPost
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
                    Destination = SpSloUrl,
                    Binding = SamlConstants.Bindings.HttpPost
                }
            });
        }

        public Task<Saml2FrontChannelResult> CreateErrorResponse(ValidatedLogoutRequest request, string errorStatusCode, string? subStatusCode, string? statusMessage, Ct ct)
        {
            LastCalledMethod = nameof(CreateErrorResponse);
            return Task.FromResult(new Saml2FrontChannelResult { Error = errorStatusCode });
        }
    }
}
