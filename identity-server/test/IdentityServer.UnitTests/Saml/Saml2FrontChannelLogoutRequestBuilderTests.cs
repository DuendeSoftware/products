// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class Saml2FrontChannelLogoutRequestBuilderTests
{
    private const string Category = "Saml2FrontChannelLogoutRequestBuilder";
    private const string IdpEntityId = "https://idp.example.com";
    private const string SpEntityId = "https://sp.example.com";
    private const string SpSloUrl = "https://sp.example.com/slo";
    private const string NameIdValue = "user@example.com";
    private const string NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";
    private const string SessionIndex = "_session-abc-123";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static Saml2FrontChannelLogoutRequestBuilder CreateBuilder()
        => new(
            TimeProvider.System,
            new SamlXmlWriter(),
            new MockSamlSigningService(TestCert.Load()));

    private static SamlServiceProvider CreateSp(SamlBinding binding = SamlBinding.HttpRedirect)
        => new()
        {
            EntityId = SpEntityId,
            Enabled = true,
            SingleLogoutServiceUrls = [new SamlEndpointType
            {
                Location = SpSloUrl,
                Binding = binding
            }]
        };

    [Fact]
    [Trait("Category", Category)]
    public async Task RedirectBinding_ProducesValidLogoutRequest()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        result.ShouldNotBeNull();
        result.Message.Binding.ShouldBe(SamlConstants.Bindings.HttpRedirect);
        result.Message.Destination.ShouldBe(SpSloUrl);
        result.RequestId.ShouldNotBeNullOrEmpty();
        result.SpEntityId.ShouldBe(SpEntityId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RedirectBinding_XmlContainsNameId()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        result.Message.Xml.OuterXml.ShouldContain(NameIdValue);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RedirectBinding_XmlContainsSessionIndex()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        result.Message.Xml.OuterXml.ShouldContain(SessionIndex);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RedirectBinding_SigningCertificateIsSet()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        result.Message.SigningCertificate.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ThrowsForPostBinding()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpPost);

        await Should.ThrowAsync<InvalidOperationException>(
            () => builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ThrowsWhenSpHasNoSingleLogoutServiceUrl()
    {
        var builder = CreateBuilder();
        var sp = new SamlServiceProvider
        {
            EntityId = SpEntityId,
            Enabled = true,
            SingleLogoutServiceUrls = []
        };

        await Should.ThrowAsync<InvalidOperationException>(
            () => builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ThrowsForUnsupportedBinding()
    {
        var builder = CreateBuilder();
        var sp = new SamlServiceProvider
        {
            EntityId = SpEntityId,
            Enabled = true,
            SingleLogoutServiceUrls = [new SamlEndpointType
            {
                Location = SpSloUrl,
                Binding = (SamlBinding)99 // unsupported binding value
            }]
        };

        await Should.ThrowAsync<InvalidOperationException>(
            () => builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task XmlContainsIssuer()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        result.Message.Xml.OuterXml.ShouldContain(IdpEntityId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task XmlContainsNameIdFormat()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        result.Message.Xml.OuterXml.ShouldContain(NameIdFormat);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task XmlIdIsValidNcName()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        var id = result.Message.Xml.GetAttribute("ID");
        id.ShouldNotBeNullOrEmpty();
        char.IsDigit(id![0]).ShouldBeFalse("XML ID must not start with a digit (xs:ID / NCName requirement)");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MessageNameIsSAMLRequest()
    {
        var builder = CreateBuilder();
        var sp = CreateSp(SamlBinding.HttpRedirect);

        var result = await builder.BuildLogoutRequestAsync(sp, NameIdValue, NameIdFormat, SessionIndex, IdpEntityId, _ct);

        result.Message.Name.ShouldBe(SamlConstants.RequestProperties.SAMLRequest);
    }
}
