// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class Saml2LoginPageResultHttpWriterTests
{
    private const string Origin = "https://idp.example.com";
    private const string BasePath = "/identity";
    private const string SpEntityId = "https://sp.example.com";
    private const string SpAcsUrl = "https://sp.example.com/acs";

    private readonly SamlEndpointOptions _defaults = new();

    private static ValidatedAuthnRequest CreateRequest() => new()
    {
        IdentityServerOptions = new IdentityServerOptions(),
        AuthnRequest = new AuthnRequest
        {
            Issuer = new NameId(SpEntityId)
        },
        Binding = SamlConstants.Bindings.HttpPost,
        Saml2Message = new InboundSaml2Message
        {
            Name = SamlConstants.RequestProperties.SAMLRequest,
            Xml = new System.Xml.XmlDocument().CreateElement(SamlConstants.RequestProperties.SAMLRequest),
            Destination = Origin + "/saml",
            Binding = SamlConstants.Bindings.HttpPost,
            RelayState = "some-relay-state"
        },
        RelayState = "some-relay-state",
        Saml2IdpEntityId = Origin,
        Saml2Sp = new SamlServiceProvider { EntityId = SpEntityId },
        AssertionConsumerService = new IndexedEndpoint { Location = SpAcsUrl, Binding = SamlBinding.HttpPost }
    };

    private static ValidatedAuthnRequest CreateIdpInitiatedRequest() => new()
    {
        IdentityServerOptions = new IdentityServerOptions(),
        AuthnRequest = null,
        Binding = SamlConstants.Bindings.HttpPost,
        RelayState = "some-relay-state",
        Saml2IdpEntityId = Origin,
        Saml2Sp = new SamlServiceProvider { EntityId = SpEntityId },
        AssertionConsumerService = new IndexedEndpoint { Location = SpAcsUrl, Binding = SamlBinding.HttpPost },
        IsIdpInitiated = true
    };

    private static Saml2LoginPageResult CreateResult(ValidatedAuthnRequest request) =>
        new(request, "/account/login", "returnUrl");

    private static (Saml2LoginPageResultHttpWriter Writer, SpySamlSigninStateStore Store) CreateWriter(
        string origin = Origin,
        string? basePath = BasePath)
    {
        var store = new SpySamlSigninStateStore();
        var urls = new MockServerUrls { Origin = origin, BasePath = basePath };
        var options = Options.Create(new IdentityServerOptions());
        var writer = new Saml2LoginPageResultHttpWriter(store, urls, TimeProvider.System, options);
        return (writer, store);
    }

    [Fact]
    public async Task ShouldStoreStateFromValidatedRequest()
    {
        var (writer, store) = CreateWriter();
        var request = CreateRequest();
        var result = CreateResult(request);
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        store.CapturedState.ShouldNotBeNull();
        store.CapturedState.ServiceProviderEntityId.ShouldBe(SpEntityId);
        store.CapturedState.AssertionConsumerService.Location.ShouldBe(SpAcsUrl);
        store.CapturedState.RelayState.ShouldBe("some-relay-state");
        store.CapturedState.IsIdpInitiated.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldReturn303()
    {
        var (writer, _) = CreateWriter();
        var result = CreateResult(CreateRequest());
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status303SeeOther);
    }

    [Fact]
    public async Task ShouldSetLocationHeader()
    {
        var (writer, _) = CreateWriter();
        var result = CreateResult(CreateRequest());
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        var location = context.Response.Headers.Location.ToString();
        var locationUri = new Uri(location);
        locationUri.Scheme.ShouldBe("https");
        locationUri.Host.ShouldBe("idp.example.com");
        locationUri.AbsolutePath.ShouldBe(BasePath + "/account/login");
    }

    [Fact]
    public async Task LocationShouldContainReturnUrlWithStateId()
    {
        var (writer, store) = CreateWriter();
        var result = CreateResult(CreateRequest());
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        var returnUrl = ExtractReturnUrl(context);
        var returnUri = new Uri(returnUrl, UriKind.RelativeOrAbsolute);
        if (!returnUri.IsAbsoluteUri)
        {
            returnUri = new Uri(new Uri(Origin), returnUrl);
        }
        var returnQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(returnUri.Query);
        returnQuery.ShouldContainKey(_defaults.StateIdParameterName);
        var stateIdValue = returnQuery[_defaults.StateIdParameterName].ToString();
        Guid.TryParse(stateIdValue, out _).ShouldBeTrue($"StateId should be a valid GUID but was '{stateIdValue}'");
        stateIdValue.ShouldBe(store.CapturedStateId!.Value.ToString());
    }

    [Fact]
    public async Task ReturnUrlShouldContainSamlCallbackPath()
    {
        var (writer, _) = CreateWriter();
        var result = CreateResult(CreateRequest());
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        var returnUrl = ExtractReturnUrl(context);
        var returnUri = new Uri(new Uri(Origin), returnUrl);
        var expectedPath = BasePath!.TrimEnd('/') + SamlConstants.Defaults.SingleSignOnCallbackPath;
        returnUri.AbsolutePath.ShouldBe(expectedPath);
    }

    [Fact]
    public async Task ShouldUseAbsoluteUrlForLocalRedirect()
    {
        var (writer, _) = CreateWriter();
        var result = CreateResult(CreateRequest());
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        var location = context.Response.Headers.Location.ToString();
        var locationUri = new Uri(location);
        // Local redirect URL (/account/login) should be converted to absolute
        locationUri.IsAbsoluteUri.ShouldBeTrue();
        locationUri.AbsolutePath.ShouldBe(BasePath + "/account/login");
    }

    [Fact]
    public void ShouldThrowWhenRedirectUrlIsNull() => Should.Throw<ArgumentNullException>(
            () => new Saml2LoginPageResult(CreateRequest(), null, "returnUrl"));

    [Fact]
    public void ShouldThrowWhenReturnUrlParameterIsNull() => Should.Throw<ArgumentNullException>(
            () => new Saml2LoginPageResult(CreateRequest(), "/account/login", null));

    private static string ExtractReturnUrl(DefaultHttpContext context)
    {
        var location = context.Response.Headers.Location.ToString();
        var uri = new Uri(location);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        query.ShouldContainKey("returnUrl");
        return query["returnUrl"].ToString();
    }

    [Fact]
    public async Task ShouldStoreNullAuthnRequestWhenIdpInitiated()
    {
        var (writer, store) = CreateWriter();
        var request = CreateIdpInitiatedRequest();
        var result = CreateResult(request);
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        store.CapturedState.ShouldNotBeNull();
        store.CapturedState.AuthnRequestData.ShouldBeNull();
        store.CapturedState.IsIdpInitiated.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldStoreAuthnRequestWhenSpInitiated()
    {
        var (writer, store) = CreateWriter();
        var request = CreateRequest();
        var result = CreateResult(request);
        var context = new DefaultHttpContext();

        await writer.WriteHttpResponse(result, context);

        store.CapturedState.ShouldNotBeNull();
        store.CapturedState.AuthnRequestData.ShouldNotBeNull();
        store.CapturedState.IsIdpInitiated.ShouldBeFalse();
    }
}
