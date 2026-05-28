// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class Saml2LogoutNotificationServiceTests
{
    private const string Category = "SAML SLO Notification Service";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private const string IdpEntityId = "https://idp.example.com/Saml2";
    private const string Sp1EntityId = "https://sp1.example.com";
    private const string Sp2EntityId = "https://sp2.example.com";

    private static SamlServiceProvider CreateSp(string entityId) => new()
    {
        EntityId = entityId,
        Enabled = true,
        SingleLogoutServiceUrls = [new SamlEndpointType
        {
            Location = $"{entityId}/Logout",
            Binding = SamlBinding.HttpRedirect
        }]
    };

    private static LogoutNotificationContext CreateContext(
        IReadOnlyCollection<SamlSpSessionData> sessions,
        string? initiatingSpEntityId = null) => new()
        {
            SubjectId = "user1",
            SessionId = "session1",
            ClientIds = [],
            SamlSessions = sessions,
            SamlInitiatingServiceProviderEntityId = initiatingSpEntityId
        };

    private static Saml2LogoutNotificationService CreateService(
        IEnumerable<SamlServiceProvider> serviceProviders,
        string issuer = IdpEntityId)
    {
        var store = new InMemorySamlServiceProviderStore(serviceProviders);
        var issuerService = new StubSaml2IssuerNameService(issuer);
        var builder = new StubFrontChannelLogoutRequestBuilder();
        var logger = NullLogger<Saml2LogoutNotificationService>.Instance;

        return new Saml2LogoutNotificationService(issuerService, store, builder, logger);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UsesCorrectSamlIssuer()
    {
        var sp = CreateSp(Sp1EntityId);
        var sessions = new[] { new SamlSpSessionData { EntityId = Sp1EntityId, NameId = "user1", SessionIndex = "idx1" } };
        var context = CreateContext(sessions);

        var builder = new StubFrontChannelLogoutRequestBuilder();
        var service = new Saml2LogoutNotificationService(
            new StubSaml2IssuerNameService(IdpEntityId),
            new InMemorySamlServiceProviderStore([sp]),
            builder,
            NullLogger<Saml2LogoutNotificationService>.Instance);

        await service.GetSamlFrontChannelLogoutsAsync(context, _ct);

        builder.CapturedIssuer.ShouldBe(IdpEntityId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ExcludesInitiatingSpFromNotifications()
    {
        var sp1 = CreateSp(Sp1EntityId);
        var sp2 = CreateSp(Sp2EntityId);
        var sessions = new[]
        {
            new SamlSpSessionData { EntityId = Sp1EntityId, NameId = "user1", SessionIndex = "idx1" },
            new SamlSpSessionData { EntityId = Sp2EntityId, NameId = "user1", SessionIndex = "idx2" }
        };

        // SP1 initiated the logout — should be excluded
        var context = CreateContext(sessions, initiatingSpEntityId: Sp1EntityId);
        var service = CreateService([sp1, sp2]);

        var messages = await service.GetSamlFrontChannelLogoutsAsync(context, _ct);

        messages.Messages.Count.ShouldBe(1);
        messages.Messages.Single().Message.Destination.ShouldBe($"{Sp2EntityId}/Logout");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NotifiesAllSpsWhenNoInitiatingSpSpecified()
    {
        var sp1 = CreateSp(Sp1EntityId);
        var sp2 = CreateSp(Sp2EntityId);
        var sessions = new[]
        {
            new SamlSpSessionData { EntityId = Sp1EntityId, NameId = "user1", SessionIndex = "idx1" },
            new SamlSpSessionData { EntityId = Sp2EntityId, NameId = "user1", SessionIndex = "idx2" }
        };

        var context = CreateContext(sessions, initiatingSpEntityId: null);
        var service = CreateService([sp1, sp2]);

        var messages = await service.GetSamlFrontChannelLogoutsAsync(context, _ct);

        messages.Messages.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsEmptyWhenNoSamlSessions()
    {
        var context = CreateContext([], initiatingSpEntityId: null);
        var service = CreateService([]);

        var messages = await service.GetSamlFrontChannelLogoutsAsync(context, _ct);

        messages.Messages.Count.ShouldBe(0);
    }

    /// <summary>
    /// Stub builder that captures the issuer passed to it and returns a minimal message.
    /// </summary>
    private sealed class StubFrontChannelLogoutRequestBuilder : ISaml2FrontChannelLogoutRequestBuilder
    {
        public string? CapturedIssuer { get; private set; }

        public Task<SamlLogoutRequestContext> BuildLogoutRequestAsync(
            SamlServiceProvider serviceProvider,
            string nameId,
            string? nameIdFormat,
            string sessionIndex,
            string issuer,
            Ct ct)
        {
            CapturedIssuer = issuer;

            var doc = new XmlDocument();
            doc.LoadXml("<LogoutRequest/>");

            var message = new OutboundSaml2Message
            {
                Name = "SAMLRequest",
                Xml = doc.DocumentElement!,
                Destination = serviceProvider.SingleLogoutServiceUrls.First().Location!,
                Binding = SamlConstants.Bindings.HttpRedirect
            };

            return Task.FromResult(new SamlLogoutRequestContext(message, "_test-id", serviceProvider.EntityId));

        }
    }
}
