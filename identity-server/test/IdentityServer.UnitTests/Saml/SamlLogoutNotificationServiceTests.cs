// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;
using UnitTests.Validation.Setup;
using SamlBinding = Duende.IdentityServer.Models.SamlBinding;

namespace UnitTests.Saml;

public class SamlLogoutNotificationServiceTests
{
    private const string Category = "SAML Logout Notification Service";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly MockUserSession _userSession = new();
    private readonly TestIssuerNameService _issuerNameService = new();

    private SamlLogoutNotificationService CreateSubject(params SamlServiceProvider[] samlServiceProviders)
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 5, 12, 0, 0, TimeSpan.Zero));
        var signer = CreateSigner();
        var frontChannelLogoutRequestBuilder = new SamlFrontChannelLogoutRequestBuilder(timeProvider, signer);

        return new SamlLogoutNotificationService(
            _issuerNameService,
            new InMemorySamlServiceProviderStore(samlServiceProviders),
            frontChannelLogoutRequestBuilder,
            NullLogger<SamlLogoutNotificationService>.Instance);
    }

    private static SamlProtocolMessageSigner CreateSigner()
    {
        var cert = CreateTestCertificate();
        var mockSigningService = new MockSamlSigningService(cert);
        return new SamlProtocolMessageSigner(mockSigningService, NullLogger<SamlProtocolMessageSigner>.Instance);
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test IdP",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        var exported = cert.Export(X509ContentType.Pfx, "test");
        return X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_when_no_service_providers_should_return_empty_list()
    {
        var context = new LogoutNotificationContext
        {
            SamlSessions = []
        };
        var subject = CreateSubject();

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_when_service_provider_not_found_should_skip_it()
    {
        var unknownEntityId = "https://unknown-sp.com";
        var context = new LogoutNotificationContext
        {
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = unknownEntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                }
            ]
        };
        var subject = CreateSubject();

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_when_service_provider_disabled_should_skip_it()
    {
        var sp = CreateServiceProvider();
        sp.Enabled = false;
        var context = new LogoutNotificationContext
        {
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = sp.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                }
            ]
        };
        var subject = CreateSubject(sp);

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_when_service_provider_has_no_single_logout_url_should_skip_it()
    {
        var sp = CreateServiceProvider();
        sp.SingleLogoutServiceUrl = null;
        var context = new LogoutNotificationContext
        {
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = sp.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                }
            ]
        };
        var subject = CreateSubject(sp);

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_when_valid_service_provider_should_build_logout_url()
    {
        var sp = CreateServiceProvider();
        var context = new LogoutNotificationContext
        {
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = sp.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                }
            ]
        };
        var subject = CreateSubject(sp);

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_when_multiple_valid_service_providers_should_build_multiple_logout_urls()
    {
        var sp1 = CreateServiceProvider("https://sp1.com");
        var sp2 = CreateServiceProvider("https://sp2.com");
        var context = new LogoutNotificationContext
        {
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = sp1.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                },
                new SamlSpSessionData
                {
                    EntityId = sp2.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session456"
                }
            ]
        };
        var subject = CreateSubject(sp1, sp2);

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.Count().ShouldBe(2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_should_use_context_saml_sessions_not_user_session()
    {
        // Regression test: Ensure we use context.SamlSessions instead of IUserSession
        // This prevents a bug where logout fails because the user session is already cleared
        var sp = CreateServiceProvider();

        var context = new LogoutNotificationContext
        {
            SubjectId = "user123",
            SessionId = "session-abc",
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = sp.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                }
            ]
        };

        var subject = CreateSubject(sp);

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_saml_front_channel_logouts_async_with_multiple_saml_sessions_should_generate_multiple_logouts()
    {
        // Regression test: Multiple SPs with different session data
        var sp1 = CreateServiceProvider("https://sp1.com");
        var sp2 = CreateServiceProvider("https://sp2.com");

        var context = new LogoutNotificationContext
        {
            SubjectId = "user123",
            SessionId = "session-abc",
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = sp1.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                },
                new SamlSpSessionData
                {
                    EntityId = sp2.EntityId,
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session456"
                }
            ]
        };

        var subject = CreateSubject(sp1, sp2);

        var result = await subject.GetSamlFrontChannelLogoutsAsync(context, _ct);

        result.Count().ShouldBe(2);
    }

    private static SamlServiceProvider CreateServiceProvider(string entityId = "https://sp.example.com") => new SamlServiceProvider
    {
        EntityId = entityId,
        DisplayName = "Test Service Provider",
        AssertionConsumerServiceUrls = [new Uri($"{entityId}/acs")],
        SingleLogoutServiceUrl = new SamlEndpointType
        {
            Binding = SamlBinding.HttpRedirect,
            Location = new Uri($"{entityId}/slo")
        },
        Enabled = true
    };
}
