// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Saml;

public class SamlLogoutCallbackProcessorTests
{
    private const string Category = "SAML Logout Callback Processor";

    private readonly UnitTests.Common.MockMessageStore<LogoutMessage> _logoutMessageStore = new();
    private readonly MockServiceProviderStore _serviceProviderStore = new();
    private readonly LogoutResponseBuilder _logoutResponseBuilder;
    private readonly SamlLogoutCallbackProcessor _subject;

    public SamlLogoutCallbackProcessorTests()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 5, 12, 0, 0, TimeSpan.Zero));
        var issuerNameService = new MockIssuerNameService { IssuerName = "https://idp.example.com" };
        _logoutResponseBuilder = new LogoutResponseBuilder(issuerNameService, timeProvider);

        _subject = new SamlLogoutCallbackProcessor(
            _logoutMessageStore,
            _serviceProviderStore,
            _logoutResponseBuilder,
            NullLogger<SamlLogoutCallbackProcessor>.Instance);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_logout_id_should_return_error()
    {
        var result = await _subject.ProcessAsync("invalid", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.Message.ShouldContain("No logout message found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_saml_service_provider_entity_id_should_return_error()
    {
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = null
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.Message.ShouldContain("does not contain SAML SP entity ID");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task service_provider_not_found_should_return_error()
    {
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = "https://unknown-sp.com",
            SamlLogoutRequestId = "_request123"
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.Message.ShouldContain("Service Provider not found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task disabled_service_provider_should_return_error()
    {
        var sp = CreateServiceProvider();
        sp.Enabled = false;
        _serviceProviderStore.ServiceProviders[sp.EntityId] = sp;
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_request123"
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.Message.ShouldContain("is disabled");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task service_provider_with_no_single_logout_url_should_return_error()
    {
        var sp = CreateServiceProvider();
        sp.SingleLogoutServiceUrl = null;
        _serviceProviderStore.ServiceProviders[sp.EntityId] = sp;
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_request123"
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.Message.ShouldContain("has no SingleLogoutServiceUrl");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_saml_logout_request_id_should_return_error()
    {
        var sp = CreateServiceProvider();
        _serviceProviderStore.ServiceProviders[sp.EntityId] = sp;
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = null
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.Message.ShouldContain("does not contain SAML logout request ID");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_request_should_return_success()
    {
        var sp = CreateServiceProvider();
        _serviceProviderStore.ServiceProviders[sp.EntityId] = sp;
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_request123",
            SamlRelayState = null
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeTrue();
        var logoutResponse = result.Value;
        logoutResponse.InResponseTo.ShouldBe("_request123");
        logoutResponse.Destination.ShouldBe(sp.SingleLogoutServiceUrl!.Location);
        logoutResponse.Status.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task relay_state_should_be_included_in_response()
    {
        var sp = CreateServiceProvider();
        _serviceProviderStore.ServiceProviders[sp.EntityId] = sp;
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_request123",
            SamlRelayState = "state456"
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeTrue();
        var logoutResponse = result.Value;
        logoutResponse.RelayState.ShouldNotBeNull();
        logoutResponse.RelayState.ShouldBe("state456");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task without_relay_state_should_have_null_relay_state_in_response()
    {
        var sp = CreateServiceProvider();
        _serviceProviderStore.ServiceProviders[sp.EntityId] = sp;
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_request123",
            SamlRelayState = null
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Value.RelayState.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_correct_issuer_in_response()
    {
        var sp = CreateServiceProvider();
        _serviceProviderStore.ServiceProviders[sp.EntityId] = sp;
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session123",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_request123"
        };
        _logoutMessageStore.Messages["logoutId123"] = new Message<LogoutMessage>(logoutMessage, DateTimeOffset.UtcNow.UtcDateTime);

        var result = await _subject.ProcessAsync("logoutId123", CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Value.Issuer.ShouldBe("https://idp.example.com");
    }

    private static SamlServiceProvider CreateServiceProvider() => new SamlServiceProvider
    {
        EntityId = "https://sp.example.com",
        DisplayName = "Test Service Provider",
        AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
        SingleLogoutServiceUrl = new SamlEndpointType
        {
            Binding = SamlBinding.HttpPost,
            Location = new Uri("https://sp.example.com/slo")
        },
        Enabled = true
    };

    private class MockServiceProviderStore : ISamlServiceProviderStore
    {
        public Dictionary<string, SamlServiceProvider> ServiceProviders { get; } = [];

        public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId)
        {
            ServiceProviders.TryGetValue(entityId, out var sp);
            return Task.FromResult(sp);
        }
    }

    private class MockIssuerNameService : IIssuerNameService
    {
        public string IssuerName { get; set; } = "https://idp.example.com";

        public Task<string> GetCurrentAsync() => Task.FromResult(IssuerName);
    }
}
