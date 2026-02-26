// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Mvc;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSigninEndpointTests
{
    private const string Category = "SAML Signin Endpoint";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_binding_redirected_signin()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
        var queryStringValues = HttpUtility.ParseQueryString(requestUrl.Query);
        queryStringValues["ReturnUrl"].ShouldBe("/saml/signin_callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_on_binding_redirected_signin_with_no_saml_request()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml/signin", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Missing 'SAMLRequest' query parameter in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_on_binding_redirected_signin_with_invalid_base64()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var invalidBase64 = "not-valid-base64!!!";

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={invalidBase64}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Invalid base64 encoding in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_on_binding_redirected_signin_with_invalid_saml_request()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml(version: "1.0");
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Should return SAML error response via HTTP-POST binding
        var errorResponse = await ExtractSamlErrorFromPostAsync(result);

        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:VersionMismatch");
        errorResponse.StatusMessage.ShouldNotBeNull();
        errorResponse.StatusMessage.ShouldContain("Only Version 2.0 is supported");
        errorResponse.Issuer.ShouldBe(Fixture.Url());
        errorResponse.InResponseTo.ShouldNotBeNullOrEmpty();
        errorResponse.AssertionConsumerServiceUrl.ShouldBe(Data.AcsUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_binding_redirected_signin_with_relay_state()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());

        var result =
            await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}&RelayState={Data.RelayState}", _ct);

        var samlSuccessResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        samlSuccessResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlSuccessResponse.RelayState.ShouldBe(Data.RelayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_form_post_signin()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var encodedRequest = ConvertToBase64Encoded(Build.AuthNRequestXml());
        var formData = new Dictionary<string, string>
        {
            { "SAMLRequest", encodedRequest }
        };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync($"/saml/signin?", content, _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
        var queryStringValues = HttpUtility.ParseQueryString(requestUrl.Query);
        queryStringValues["ReturnUrl"].ShouldBe("/saml/signin_callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_on_form_post_signin_with_no_saml_request()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>());

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Missing 'SAMLRequest' form parameter in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_on_form_post_signin_with_invalid_base64()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var invalidBase64 = "not-valid-base64!!!";
        var formData = new Dictionary<string, string>
        {
            { "SAMLRequest", invalidBase64 }
        };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Invalid base64 encoding in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_on_binding_form_post_with_invalid_saml_request()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml();
        var encodedRequest = ConvertToBase64Encoded(Build.AuthNRequestXml(version: "1.0"));
        var formData = new Dictionary<string, string>
        {
            { "SAMLRequest", encodedRequest }
        };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync($"/saml/signin?", content, _ct);

        // Should return SAML error response via HTTP-POST binding
        var errorResponse = await ExtractSamlErrorFromPostAsync(result);

        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:VersionMismatch");
        errorResponse.StatusMessage.ShouldNotBeNull();
        errorResponse.StatusMessage.ShouldContain("Only Version 2.0 is supported");
        errorResponse.Issuer.ShouldBe(Fixture.Url());
        errorResponse.InResponseTo.ShouldNotBeNullOrEmpty();
        errorResponse.AssertionConsumerServiceUrl.ShouldBe(Data.AcsUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_form_post_signin_with_relay_state()
    {
        Data.RelayState = "test_relay_state";
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var encodedRequest = ConvertToBase64Encoded(Build.AuthNRequestXml());
        var formData = new Dictionary<string, string>
        {
            { "SAMLRequest", encodedRequest },
            { "RelayState", Data.RelayState }
        };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync($"/saml/signin?", content, _ct);

        var samlSuccessResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        samlSuccessResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlSuccessResponse.RelayState.ShouldBe(Data.RelayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_request_is_within_clock_skew_range()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Create a request with IssueInstant 2 minutes in the future (within default 5 minute clock skew)
        var slightlyFutureTime = Data.Now.AddMinutes(2);
        var authnRequestXml = Build.AuthNRequestXml(issueInstant: slightlyFutureTime);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Should succeed and redirect to login
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_request_issue_instant_is_in_future()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Create a request with IssueInstant 10 minutes in the future (beyond default clock skew)
        var futureTime = Data.Now.AddMinutes(10);
        var authnRequestXml = Build.AuthNRequestXml(futureTime);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldNotBeNull();
        errorResponse.StatusMessage.ShouldContain("IssueInstant is in the future");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_request_issue_instant_exceeds_service_provider_configured_clock_skew()
    {
        var serviceProviderClockSkew = TimeSpan.FromMinutes(2);
        var sp = Build.SamlServiceProvider();
        sp.ClockSkew = serviceProviderClockSkew;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var futureIssueInstant = Data.Now.Add(serviceProviderClockSkew) + TimeSpan.FromSeconds(1);
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(issueInstant: futureIssueInstant));

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);

        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldBe("Request IssueInstant is in the future");
        errorResponse.Issuer.ShouldBe(Fixture.Url());
        errorResponse.InResponseTo.ShouldNotBeNullOrEmpty();
        errorResponse.AssertionConsumerServiceUrl.ShouldBe(Data.AcsUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_request_is_expired()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Create a request with IssueInstant 10 minutes in the past (beyond default max age)
        var pastTime = Data.Now.AddMinutes(-10);
        var authnRequestXml = Build.AuthNRequestXml(pastTime);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldNotBeNull();
        errorResponse.StatusMessage.ShouldContain("expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_request_is_expired_with_custom_request_max_age()
    {
        var sp = Build.SamlServiceProvider();
        sp.RequestMaxAge = TimeSpan.FromMinutes(20);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var pastTime = Data.Now.AddMinutes(-21);
        var authnRequestXml = Build.AuthNRequestXml(pastTime);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldNotBeNull();
        errorResponse.StatusMessage.ShouldContain("expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_destination_is_invalid()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Use an invalid destination URL
        var authnRequestXml = Build.AuthNRequestXml(destination: new Uri("https://wrong.example.com/saml/sso"));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldNotBeNull();
        errorResponse.StatusMessage.ShouldContain("Invalid destination");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_url_is_not_registered()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Use an ACS URL that's not registered with the SP
        var authnRequestXml = Build.AuthNRequestXml(acsUrl: new Uri("https://sp.example.com/wrong-callback"));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail.ShouldContain("AssertionConsumerServiceUrl");
        problemDetails.Detail.ShouldContain("is not valid");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_index_is_invalid()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Add an AssertionConsumerServiceIndex that doesn't exist
        var authnRequestXml = Build.AuthNRequestXml()
            .Replace("AssertionConsumerServiceURL=\"" + Data.AcsUrl + "\"",
                "AssertionConsumerServiceIndex=\"99\"");

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail.ShouldContain("AssertionConsumerServiceIndex");
        problemDetails.Detail.ShouldContain("is not valid");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_service_provider_has_no_configured_acs_urls()
    {
        var sp = Build.SamlServiceProvider();
        sp.AssertionConsumerServiceUrls = [];
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml()
            .Replace("AssertionConsumerServiceURL=\"" + Data.AcsUrl + "\"",
                string.Empty);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail.ShouldContain(
            $"The Service Provider '{Data.EntityId}' does not have any configured Assertion Consumer Service URLs");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_service_provider_is_not_enabled()
    {
        var sp = Build.SamlServiceProvider();
        sp.Enabled = false;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml();

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail.ShouldContain($"Service Provider '{Data.EntityId}' is not registered or is disabled");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_request_uses_default_acs_url()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml()
            .Replace("AssertionConsumerServiceURL=\"" + Data.AcsUrl + "\"",
                string.Empty);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_destination_matches()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Use the correct Destination attribute
        var correctDestination = new Uri(Fixture.Url() + "/saml/signin");
        var authnRequestXml = Build.AuthNRequestXml(destination: correctDestination);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Should succeed and redirect to login
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_user_is_already_logged_in_at_identity_provider()
    {
        var sp = Build.SamlServiceProvider();
        sp.ClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["sub"] = "sub",
            ["idp"] = "idp",
            ["amr"] = "amr",
            ["auth_time"] = "auth_time"
        });
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);

        successResponse.ResponseId.ShouldNotBeNullOrEmpty();
        successResponse.Destination.ShouldBe(Fixture.Data.AcsUrl.ToString());
        successResponse.IssueInstant.ShouldBe(Data.FakeTimeProvider.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        successResponse.Issuer.ShouldBe(Fixture.Url());
        successResponse.InResponseTo.ShouldBe(Data.RequestId);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);

        var assertion = successResponse.Assertion;
        assertion.ShouldNotBeNull();
        assertion.Id.ShouldNotBeNullOrEmpty();
        assertion.Version.ShouldBe("2.0");
        assertion.IssueInstant.ShouldBe(Data.FakeTimeProvider.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        assertion.Issuer.ShouldBe(Fixture.Url());

        var subject = assertion.Subject;
        subject.ShouldNotBeNull();
        subject.NameId.ShouldBe("user-id");
        subject.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Unspecified);
        subject.SPNameQualifier.ShouldBeNull();

        var subjectConfirmation = subject.SubjectConfirmation;
        subjectConfirmation.ShouldNotBeNull();
        subjectConfirmation.Method.ShouldBe("urn:oasis:names:tc:SAML:2.0:cm:bearer");

        var subjectConfirmationData = subjectConfirmation.SubjectConfirmationData;
        subjectConfirmationData.ShouldNotBeNull();
        subjectConfirmationData.NotOnOrAfter.ShouldBe(Data.FakeTimeProvider.GetUtcNow().Add(Build.SamlServiceProvider().RequestMaxAge!.Value).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        subjectConfirmationData.Recipient.ShouldBe(Fixture.Data.AcsUrl.ToString());
        subjectConfirmationData.InResponseTo.ShouldBe(Data.RequestId);

        var conditions = assertion.Conditions;
        conditions.ShouldNotBeNull();
        conditions.NotBefore.ShouldBe(Data.FakeTimeProvider.GetUtcNow().Subtract(Build.SamlServiceProvider().ClockSkew!.Value).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        conditions.NotOnOrAfter.ShouldBe(Data.FakeTimeProvider.GetUtcNow().Add(Build.SamlServiceProvider().RequestMaxAge!.Value).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        conditions.Audience.ShouldBe(Data.EntityId.ToString());

        var authnStatement = assertion.AuthnStatement;
        authnStatement.ShouldNotBeNull();
        authnStatement.AuthnInstant.ShouldBe(Data.FakeTimeProvider.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        authnStatement.SessionIndex.ShouldNotBeNullOrEmpty();
        authnStatement.AuthnContextClassRef.ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:unspecified");

        var attributes = assertion.Attributes;
        attributes.ShouldNotBeNull();
        attributes.Count.ShouldBe(4);

        var subAttribute = attributes.FirstOrDefault(a => a.Name == "sub");
        subAttribute.ShouldNotBeNull();
        subAttribute.NameFormat.ShouldBe(SamlConstants.AttributeNameFormats.Uri);
        subAttribute.FriendlyName.ShouldBe("sub");
        subAttribute.Value.ShouldBe("user-id");

        var idpAttribute = attributes.FirstOrDefault(a => a.Name == "idp");
        idpAttribute.ShouldNotBeNull();
        idpAttribute.NameFormat.ShouldBe(SamlConstants.AttributeNameFormats.Uri);
        idpAttribute.FriendlyName.ShouldBe("idp");
        idpAttribute.Value.ShouldBe("local");

        var amrAttribute = attributes.FirstOrDefault(a => a.Name == "amr");
        amrAttribute.ShouldNotBeNull();
        amrAttribute.NameFormat.ShouldBe(SamlConstants.AttributeNameFormats.Uri);
        amrAttribute.FriendlyName.ShouldBe("amr");
        amrAttribute.Value.ShouldBe("pwd");

        var authTimeAttribute = attributes.FirstOrDefault(a => a.Name == "auth_time");
        authTimeAttribute.ShouldNotBeNull();
        authTimeAttribute.NameFormat.ShouldBe(SamlConstants.AttributeNameFormats.Uri);
        authTimeAttribute.FriendlyName.ShouldBe("auth_time");
        authTimeAttribute.Value.ShouldBe(Data.FakeTimeProvider.GetUtcNow().ToUnixTimeSeconds().ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_user_is_already_logged_in_at_identity_provider_but_request_includes_force_authn()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(forceAuthn: true);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    private async Task<string> EncodeRequest(string authenticationRequest)
    {
        var bytes = Encoding.UTF8.GetBytes(authenticationRequest);
        using var outputStream = new MemoryStream();
        await using (var deflateStream = new DeflateStream(outputStream, CompressionMode.Compress, leaveOpen: true))
        {
            await deflateStream.WriteAsync(bytes, 0, bytes.Length, _ct);
        }

        var compressedBytes = outputStream.ToArray();
        var base64 = Convert.ToBase64String(compressedBytes);
        var urlEncoded = Uri.EscapeDataString(base64);
        return urlEncoded;
    }

    private string ConvertToBase64Encoded(string authenticationRequest) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationRequest));

    /// <summary>
    /// Extracts SAML error response from an HTTP-POST binding auto-submit form.
    /// </summary>
    private async Task<SamlErrorResponseData> ExtractSamlErrorFromPostAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        var html = await response.Content.ReadAsStringAsync(_ct);

        // Extract SAMLResponse from hidden input field
        var samlResponseMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<input[^>]+name=""SAMLResponse""[^>]+value=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        samlResponseMatch.Success.ShouldBeTrue("SAMLResponse input field not found in HTML");
        var encodedResponse = samlResponseMatch.Groups[1].Value;

        // Extract RelayState if present
        string? relayState = null;
        var relayStateMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<input[^>]+name=""RelayState""[^>]+value=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (relayStateMatch.Success)
        {
            relayState = HttpUtility.HtmlDecode(relayStateMatch.Groups[1].Value);
        }

        // Extract form action (ACS URL)
        var actionMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<form[^>]+action=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        actionMatch.Success.ShouldBeTrue("Form action not found in HTML");
        var acsUrl = HttpUtility.HtmlDecode(actionMatch.Groups[1].Value);

        // Decode the SAML response
        var decodedBytes = Convert.FromBase64String(HttpUtility.HtmlDecode(encodedResponse));
        var responseXml = Encoding.UTF8.GetString(decodedBytes);

        // Parse the SAML response XML
        var doc = System.Xml.Linq.XDocument.Parse(responseXml);
        var samlpNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:protocol");
        var samlNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:assertion");

        var responseElement = doc.Root;
        responseElement.ShouldNotBeNull();
        responseElement.Name.ShouldBe(samlpNs + "Response");

        var responseId = responseElement.Attribute("ID")?.Value;
        var inResponseTo = responseElement.Attribute("InResponseTo")?.Value;
        var destination = responseElement.Attribute("Destination")?.Value;
        var issueInstant = responseElement.Attribute("IssueInstant")?.Value;

        var issuer = responseElement.Element(samlNs + "Issuer")?.Value;

        var statusElement = responseElement.Element(samlpNs + "Status");
        statusElement.ShouldNotBeNull();

        var statusCodeElement = statusElement.Element(samlpNs + "StatusCode");
        statusCodeElement.ShouldNotBeNull();
        var statusCode = statusCodeElement.Attribute("Value")?.Value;

        var statusMessage = statusElement.Element(samlpNs + "StatusMessage")?.Value;

        // Check for sub-status code
        var subStatusCodeElement = statusCodeElement.Element(samlpNs + "StatusCode");
        var subStatusCode = subStatusCodeElement?.Attribute("Value")?.Value;

        return new SamlErrorResponseData
        {
            ResponseId = responseId,
            InResponseTo = inResponseTo,
            Destination = destination,
            IssueInstant = issueInstant,
            Issuer = issuer,
            StatusCode = statusCode,
            StatusMessage = statusMessage,
            SubStatusCode = subStatusCode,
            RelayState = relayState,
            AssertionConsumerServiceUrl = acsUrl
        };
    }

    private record SamlErrorResponseData
    {
        public string? ResponseId { get; init; }
        public string? InResponseTo { get; init; }
        public string? Destination { get; init; }
        public string? IssueInstant { get; init; }
        public string? Issuer { get; init; }
        public string? StatusCode { get; init; }
        public string? StatusMessage { get; init; }
        public string? SubStatusCode { get; init; }
        public string? RelayState { get; init; }
        public string? AssertionConsumerServiceUrl { get; init; }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task redirects_to_consent_when_require_consent_is_true()
    {
        var sp = Build.SamlServiceProvider();
        sp.RequireConsent = true;
        Fixture.ServiceProviders.Add(sp);

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.ConsentUrl.ToString());
        var queryStringValues = HttpUtility.ParseQueryString(requestUrl.Query);
        queryStringValues["ReturnUrl"].ShouldBe("/saml/signin_callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_is_passive_and_user_not_authenticated()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(isPassive: true));

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);

        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:NoPassive");
        errorResponse.StatusMessage.ShouldBe("The user is not currently logged in and passive login was requested.");
        errorResponse.Issuer.ShouldBe(Fixture.Url());
        errorResponse.InResponseTo.ShouldNotBeNullOrEmpty();
        errorResponse.AssertionConsumerServiceUrl.ShouldBe(Data.AcsUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_no_passive_error_when_both_force_authn_and_is_passive_are_true()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(forceAuthn: true, isPassive: true);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);

        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:NoPassive");
        errorResponse.StatusMessage.ShouldBe("The user is not currently logged in");
        errorResponse.Issuer.ShouldBe(Fixture.Url());
        errorResponse.InResponseTo.ShouldNotBeNullOrEmpty();
        errorResponse.AssertionConsumerServiceUrl.ShouldBe(Data.AcsUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_is_passive_true_and_user_authenticated()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(isPassive: true);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task forces_authentication_when_force_authn_true_and_user_authenticated()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        // Verify user is authenticated by doing a normal request first
        var normalRequestXml = Build.AuthNRequestXml(forceAuthn: false);
        var normalUrlEncoded = await EncodeRequest(normalRequestXml);
        var normalResult = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={normalUrlEncoded}", _ct);

        // Without ForceAuthn, authenticated user goes directly to callback
        normalResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        var samlSuccessResponse = await ExtractSamlSuccessFromPostAsync(normalResult, _ct);
        samlSuccessResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);

        var forceAuthnRequestXml = Build.AuthNRequestXml(forceAuthn: true);
        var forceAuthnUrlEncoded = await EncodeRequest(forceAuthnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={forceAuthnUrlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());

        var queryStringValues = HttpUtility.ParseQueryString(requestUrl.Query);
        var returnUrl = queryStringValues["ReturnUrl"];
        returnUrl.ShouldBe("/saml/signin_callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_issue_instant_at_exact_clock_skew_boundary()
    {
        var clockSkew = TimeSpan.FromMinutes(5);
        var sp = Build.SamlServiceProvider();
        sp.ClockSkew = clockSkew;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var exactBoundaryTime = Data.Now.Add(clockSkew);
        var authnRequestXml = Build.AuthNRequestXml(issueInstant: exactBoundaryTime);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_issue_instant_one_second_beyond_clock_skew()
    {
        var clockSkew = TimeSpan.FromMinutes(5);
        var sp = Build.SamlServiceProvider();
        sp.ClockSkew = clockSkew;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var beyondBoundaryTime = Data.Now.Add(clockSkew).AddSeconds(1);
        var authnRequestXml = Build.AuthNRequestXml(issueInstant: beyondBoundaryTime);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldBe("Request IssueInstant is in the future");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_clock_skew_is_zero()
    {
        var sp = Build.SamlServiceProvider();
        sp.ClockSkew = TimeSpan.Zero;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var exactTime = Data.Now;
        var authnRequestXml = Build.AuthNRequestXml(issueInstant: exactTime);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_clock_skew_is_zero_and_time_differs()
    {
        var sp = Build.SamlServiceProvider();
        sp.ClockSkew = TimeSpan.Zero;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var futureTime = Data.Now.AddSeconds(1);
        var authnRequestXml = Build.AuthNRequestXml(issueInstant: futureTime);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldBe("Request IssueInstant is in the future");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_issue_instant_at_max_age_boundary()
    {
        var maxAge = TimeSpan.FromMinutes(5);
        var sp = Build.SamlServiceProvider();
        sp.RequestMaxAge = maxAge;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var exactMaxAgeTime = Data.Now.Subtract(maxAge);
        var authnRequestXml = Build.AuthNRequestXml(issueInstant: exactMaxAgeTime);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_issue_instant_one_second_beyond_max_age()
    {
        var maxAge = TimeSpan.FromMinutes(5);
        var sp = Build.SamlServiceProvider();
        sp.RequestMaxAge = maxAge;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var beyondMaxAgeTime = Data.Now.Subtract(maxAge).AddSeconds(-1);
        var authnRequestXml = Build.AuthNRequestXml(issueInstant: beyondMaxAgeTime);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester");
        errorResponse.StatusMessage.ShouldBe("Request has expired (IssueInstant too old)");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task uses_acs_url_when_both_url_and_index_provided()
    {
        var primaryAcsUrl = new Uri("https://sp.example.com/callback1");
        var secondaryAcsUrl = new Uri("https://sp.example.com/callback2");

        var sp = Build.SamlServiceProvider();
        sp.AssertionConsumerServiceUrls = [primaryAcsUrl, secondaryAcsUrl];
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml(
            acsUrl: primaryAcsUrl,
            acsIndex: 1  // Points to secondary URL
        );
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());

        var queryStringValues = HttpUtility.ParseQueryString(requestUrl.Query);
        var returnUrl = queryStringValues["ReturnUrl"];
        returnUrl.ShouldBe("/saml/signin_callback");

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var signinCallbackResponse = await Fixture.Client.GetAsync(returnUrl, _ct);

        var samlSuccessResponse = await ExtractSamlSuccessFromPostAsync(signinCallbackResponse, _ct);
        samlSuccessResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlSuccessResponse.Destination.ShouldBe(primaryAcsUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_request_has_no_id_attribute()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml();
        var xmlWithoutId = authnRequestXml.Replace($"ID=\"{Data.RequestId}\"", "");

        var urlEncoded = await EncodeRequest(xmlWithoutId);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        //TODO: do we want more specific errors returned?
        problemDetails.Detail.ShouldBe("Invalid SAMLRequest format in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_request_has_empty_id()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml(requestId: "");
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldNotBeNull();
        //TODO: do we want more specific errors returned?
        problemDetails.Detail.ShouldBe("Invalid SAMLRequest format in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_request_issuer_is_empty()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml(issuer: "");
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        //TODO: do we want more specific errors returned?
        problemDetails.Detail.ShouldBe("Invalid SAMLRequest format in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_request_issuer_is_missing()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml();
        var xmlWithoutIssuer = authnRequestXml.Replace(
            $"<saml:Issuer>{Data.EntityId}</saml:Issuer>",
            ""
        );

        var urlEncoded = await EncodeRequest(xmlWithoutIssuer);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        //TODO: do we want more specific errors returned?
        problemDetails.Detail.ShouldBe("Invalid SAMLRequest format in SAML signin request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_response_is_signed_with_sign_response_behavior()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: false);

        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);
        VerifySignaturePositionAfterIssuer(responseElement);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_response_is_signed_with_sign_assertion_behavior()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignAssertion;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: true);

        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        var assertionElement = responseElement.Element(samlNs + "Assertion");
        assertionElement.ShouldNotBeNull();
        VerifySignaturePositionAfterIssuer(assertionElement!);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_response_is_signed_with_sign_both_behavior()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignBoth;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: true);

        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        VerifySignaturePositionAfterIssuer(responseElement);

        var assertionElement = responseElement.Element(samlNs + "Assertion");
        assertionElement.ShouldNotBeNull();
        VerifySignaturePositionAfterIssuer(assertionElement!);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_response_is_not_signed_with_do_not_sign_behavior()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.DoNotSign;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: false);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_response_uses_default_signing_behavior_with_null()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = null;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        // Default behavior should be SignAssertion per SAML best practices
        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: true);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task response_signature_contains_correct_reference_to_response_id()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        // Extract signature and verify Reference URI points to Response ID
        var signatureElement = ExtractSignatureElement(responseElement);
        signatureElement.ShouldNotBeNull("Response should have a Signature element");

        var referenceUri = GetSignatureReferenceUri(signatureElement!);
        referenceUri.ShouldNotBeNull();
        referenceUri.ShouldStartWith("#");

        var responseId = responseElement.Attribute("ID")?.Value;
        responseId.ShouldNotBeNullOrEmpty();
        referenceUri.ShouldBe($"#{responseId}");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task assertion_signature_contains_correct_reference_to_assertion_id()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignAssertion;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);
        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);

        // Extract Assertion and its signature
        var assertionElement = responseElement.Element(samlNs + "Assertion");
        assertionElement.ShouldNotBeNull("Response should contain an Assertion");

        var signatureElement = ExtractSignatureElement(assertionElement!);
        signatureElement.ShouldNotBeNull();

        var referenceUri = GetSignatureReferenceUri(signatureElement!);
        referenceUri.ShouldNotBeNull();
        referenceUri.ShouldStartWith("#");

        var assertionId = assertionElement!.Attribute("ID")?.Value;
        assertionId.ShouldNotBeNullOrEmpty();
        referenceUri.ShouldBe($"#{assertionId}");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task response_signature_contains_key_info_with_certificate()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        // Extract signature and verify KeyInfo structure
        var signatureElement = ExtractSignatureElement(responseElement);
        signatureElement.ShouldNotBeNull();

        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        var keyInfoElement = signatureElement!.Element(dsNs + "KeyInfo");
        keyInfoElement.ShouldNotBeNull("Signature should contain KeyInfo element");

        var x509DataElement = keyInfoElement!.Element(dsNs + "X509Data");
        x509DataElement.ShouldNotBeNull("KeyInfo should contain X509Data element");

        var x509CertificateElement = x509DataElement!.Element(dsNs + "X509Certificate");
        x509CertificateElement.ShouldNotBeNull("X509Data should contain X509Certificate element");

        var certificateData = x509CertificateElement!.Value;
        certificateData.ShouldNotBeNullOrEmpty("X509Certificate should contain certificate data");

        var signingCert = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(SamlFixture.StableSigningCert), null);
        certificateData.ShouldBe(Convert.ToBase64String(signingCert.Export(X509ContentType.Cert)));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task response_signature_uses_sha256_digest_method()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        // Parse signature algorithms
        var signatureElement = ExtractSignatureElement(responseElement);
        signatureElement.ShouldNotBeNull();

        var signatureInfo = ParseSignatureInfo(signatureElement!);

        // Verify SHA256 digest method
        signatureInfo.DigestMethod.ShouldNotBeNull("Signature should specify DigestMethod");
        signatureInfo.DigestMethod.ShouldBe("http://www.w3.org/2001/04/xmlenc#sha256",
            "DigestMethod should be SHA256");

        // Verify RSA-SHA256 signature method
        signatureInfo.SignatureMethod.ShouldNotBeNull("Signature should specify SignatureMethod");
        signatureInfo.SignatureMethod.ShouldBe("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
            "SignatureMethod should be RSA-SHA256");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task response_signature_uses_exclusive_canonicalization()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        // Parse signature canonicalization
        var signatureElement = ExtractSignatureElement(responseElement);
        signatureElement.ShouldNotBeNull();

        var signatureInfo = ParseSignatureInfo(signatureElement!);

        // Verify Exclusive Canonicalization (C14N)
        signatureInfo.CanonicalizationMethod.ShouldNotBeNull("Signature should specify CanonicalizationMethod");
        signatureInfo.CanonicalizationMethod.ShouldBe("http://www.w3.org/2001/10/xml-exc-c14n#",
            "CanonicalizationMethod should be Exclusive C14N");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task assertion_signature_uses_correct_algorithms()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignAssertion;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);
        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);

        // Extract Assertion signature and verify algorithms
        var assertionElement = responseElement.Element(samlNs + "Assertion");
        assertionElement.ShouldNotBeNull();

        var signatureElement = ExtractSignatureElement(assertionElement!);
        signatureElement.ShouldNotBeNull();

        var signatureInfo = ParseSignatureInfo(signatureElement!);

        // Verify all algorithms match SAML 2.0 best practices
        signatureInfo.CanonicalizationMethod.ShouldBe("http://www.w3.org/2001/10/xml-exc-c14n#");
        signatureInfo.SignatureMethod.ShouldBe("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");
        signatureInfo.DigestMethod.ShouldBe("http://www.w3.org/2001/04/xmlenc#sha256");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_both_uses_consistent_algorithms_for_both_signatures()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignBoth;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);
        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);

        // Extract and verify Response signature
        var responseSignature = ExtractSignatureElement(responseElement);
        responseSignature.ShouldNotBeNull();
        var responseSignatureInfo = ParseSignatureInfo(responseSignature!);

        // Extract and verify Assertion signature
        var assertionElement = responseElement.Element(samlNs + "Assertion");
        assertionElement.ShouldNotBeNull();
        var assertionSignature = ExtractSignatureElement(assertionElement!);
        assertionSignature.ShouldNotBeNull();
        var assertionSignatureInfo = ParseSignatureInfo(assertionSignature!);

        // Both signatures should use the same algorithms
        responseSignatureInfo.CanonicalizationMethod.ShouldBe(assertionSignatureInfo.CanonicalizationMethod,
            "Both signatures should use same canonicalization method");
        responseSignatureInfo.SignatureMethod.ShouldBe(assertionSignatureInfo.SignatureMethod,
            "Both signatures should use same signature method");
        responseSignatureInfo.DigestMethod.ShouldBe(assertionSignatureInfo.DigestMethod,
            "Both signatures should use same digest method");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task signed_response_includes_relay_state()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);
        var relayStateValue = "test-relay-state-123";

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}&RelayState={relayStateValue}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: false);

        successResponse.RelayState.ShouldBe(relayStateValue);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task signed_response_works_with_force_authn()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var normalRequestXml = Build.AuthNRequestXml(forceAuthn: false);
        var normalUrlEncoded = await EncodeRequest(normalRequestXml);
        var normalResult = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={normalUrlEncoded}", _ct);
        var normalResponse = await ExtractSamlSuccessFromPostAsync(normalResult, _ct);
        normalResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);

        var forceAuthnRequestXml = Build.AuthNRequestXml(forceAuthn: true);
        var forceAuthnUrlEncoded = await EncodeRequest(forceAuthnRequestXml);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={forceAuthnUrlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());

        var queryStringValues = HttpUtility.ParseQueryString(requestUrl.Query);
        var returnUrl = queryStringValues["ReturnUrl"];
        returnUrl.ShouldBe("/saml/signin_callback");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task signed_response_works_with_is_passive()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignBoth;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(isPassive: true);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: true);

        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task signed_response_works_with_custom_acs_index()
    {
        var primaryAcsUrl = new Uri("https://sp.example.com/callback1");
        var secondaryAcsUrl = new Uri("https://sp.example.com/callback2");

        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        sp.AssertionConsumerServiceUrls = [primaryAcsUrl, secondaryAcsUrl];
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(acsIndex: 1);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: false);

        successResponse.Destination.ShouldBe(secondaryAcsUrl.ToString());
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signature_required_but_request_not_signed_redirect_binding()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Missing signature parameter");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signature_required_but_request_not_signed_post_binding()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var encodedRequest = ConvertToBase64Encoded(Build.AuthNRequestXml());
        var formData = new Dictionary<string, string> { { "SAMLRequest", encodedRequest } };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Signature element not found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signature_required_but_signature_invalid_redirect_binding()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var wrongCert = CreateTestSigningCertificate(Data.FakeTimeProvider, "CN=Wrong Certificate");
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, wrongCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Invalid signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signature_required_but_signature_invalid_post_binding()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var wrongCert = CreateTestSigningCertificate(Data.FakeTimeProvider, "CN=Wrong Certificate");
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var signedXml = SignAuthNRequestXml(Build.AuthNRequestXml(), wrongCert);
        var encodedRequest = ConvertToBase64Encoded(signedXml);
        var formData = new Dictionary<string, string> { { "SAMLRequest", encodedRequest } };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Invalid signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signature_required_but_no_sp_certificates_configured()
    {
        var sp = Build.SamlServiceProvider(signingCertificate: null, requireSignedAuthnRequests: true);
        Fixture.ServiceProviders.Add(sp);

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{sp.EntityId}' has no signing certificates configured and has sent a SAML signin request which requires signature validation");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_redirect_binding_request_correctly_signed()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, signingCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_post_binding_request_correctly_signed()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var signedXml = SignAuthNRequestXml(Build.AuthNRequestXml(), signingCert);
        var encodedRequest = ConvertToBase64Encoded(signedXml);
        var formData = new Dictionary<string, string> { { "SAMLRequest", encodedRequest } };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_signature_optional_and_request_not_signed()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(requireSignedAuthnRequests: false));

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_signature_optional_and_request_is_signed()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: false));

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, signingCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_multiple_certificates_and_one_matches()
    {
        var cert1 = CreateTestSigningCertificate(Data.FakeTimeProvider, "CN=Certificate 1");
        var cert2 = CreateTestSigningCertificate(Data.FakeTimeProvider, "CN=Certificate 2");
        var cert3 = CreateTestSigningCertificate(Data.FakeTimeProvider, "CN=Certificate 3");

        var sp = new SamlServiceProvider
        {
            EntityId = Data.EntityId,
            DisplayName = "Example SP",
            Description = "Example SP",
            Enabled = true,
            RequireSignedAuthnRequests = true,
            SigningCertificates = [cert1, cert2, cert3],
            AssertionConsumerServiceUrls = [Data.AcsUrl],
            AssertionConsumerServiceBinding = SamlBinding.HttpPost,
            RequestMaxAge = TimeSpan.FromMinutes(5),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, cert2);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_redirect_binding_signature_algorithm_unsupported()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        // Sign with SHA1 (deprecated/unsupported)
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, signingCert,
            "http://www.w3.org/2000/09/xmldsig#rsa-sha1");

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Unsupported signature algorithm: http://www.w3.org/2000/09/xmldsig#rsa-sha1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_redirect_binding_signature_has_query_order_incorrect()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var relayState = "relayState";
        // Pass urlEncoded and relayState in incorrect order for signing to cause a bad signature
        var (signature, sigAlg) = SignAuthNRequestRedirect(relayState, urlEncoded, signingCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&SigAlg={Uri.EscapeDataString(sigAlg)}&Signature={Uri.EscapeDataString(signature)}", _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Invalid signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_redirect_binding_includes_relay_state_in_signature()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var relayState = "test-relay-state-value";
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, relayState, signingCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&RelayState={Uri.EscapeDataString(relayState)}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.RelayState.ShouldBe(relayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_post_binding_signature_element_has_empty_reference()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var authNRequestXml = Build.AuthNRequestXml();
        var signedXml = SignAuthNRequestXmlWithEmptyReference(authNRequestXml, signingCert);
        var encodedRequest = ConvertToBase64Encoded(signedXml);
        var formData = new Dictionary<string, string> { { "SAMLRequest", encodedRequest } };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Invalid signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_post_binding_signature_reference_wrong_id()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var authNRequestXml = Build.AuthNRequestXml();
        var signedXml = SignAuthNRequestXml(authNRequestXml, signingCert);
        signedXml = signedXml.Replace($"#{Fixture.Data.RequestId}", "#_bogus_id");
        var encodedRequest = ConvertToBase64Encoded(signedXml);
        var formData = new Dictionary<string, string> { { "SAMLRequest", encodedRequest } };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        samlError.StatusMessage.ShouldBe("Invalid signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_post_binding_signature_uses_exclusive_canonicalization()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        // SignAuthNRequestXml already uses ExcC14N, so this validates it works
        var signedXml = SignAuthNRequestXml(Build.AuthNRequestXml(), signingCert);
        var encodedRequest = ConvertToBase64Encoded(signedXml);
        var formData = new Dictionary<string, string> { { "SAMLRequest", encodedRequest } };
        var content = new FormUrlEncodedContent(formData);

        var result = await Fixture.Client.PostAsync("/saml/signin", content, _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signature_certificate_expired()
    {
        var expiredCert = CreateExpiredTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(expiredCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, expiredCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Responder);
        samlError.StatusMessage.ShouldBe("No valid certificates configured for service provider");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signature_certificate_not_yet_valid()
    {
        var notYetValidCert = CreateNotYetValidTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(notYetValidCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, notYetValidCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Responder);
        samlError.StatusMessage.ShouldBe("No valid certificates configured for service provider");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_signature_certificate_within_validity_period()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());
        var (signature, sigAlg) = SignAuthNRequestRedirect(urlEncoded, null, signingCert);

        var result = await Fixture.Client.GetAsync(
            $"/saml/signin?SAMLRequest={urlEncoded}&Signature={Uri.EscapeDataString(signature)}&SigAlg={Uri.EscapeDataString(sigAlg)}", _ct);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task auth_n_request_with_requested_authn_context_and_requirement_is_satisfied()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="exact">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(requestedAuthnContext: requestedAuthnContext));

        // First, initiate SAML signin to create state (use NonRedirectingClient for all requests to share cookies)
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);
        signinResult.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        // Then sign in the user with authn context requirements
        Fixture.UserMetRequestedAuthnContextRequirements = true;
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim("saml:acr", "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport")
        ], "Test"));

        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Act - complete the callback
        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.AuthnStatement?.AuthnContextClassRef.ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task auth_n_request_with_requested_authn_context_and_requirement_is_not_satisfied()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="exact">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(requestedAuthnContext: requestedAuthnContext));

        // First, initiate SAML signin to create state (use NonRedirectingClient for all requests to share cookies)
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);
        signinResult.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        // Then sign in the user with authn context requirements
        Fixture.UserMetRequestedAuthnContextRequirements = false;
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim("saml:acr", "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport")
        ], "Test"));

        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Act - complete the callback
        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", _ct);

        // Assert
        var samlResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.SubStatusCode.ShouldBe(SamlStatusCodes.NoAuthnContext);
        samlResponse.Assertion.AuthnStatement?.AuthnContextClassRef.ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task auth_n_request_with_requested_authn_context_but_no_claim_returns_error()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="minimum">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(requestedAuthnContext: requestedAuthnContext));

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var samlResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.SubStatusCode.ShouldBe(SamlStatusCodes.NoAuthnContext);
        samlResponse.Assertion.AuthnStatement?.AuthnContextClassRef.ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:unspecified");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task auth_n_request_without_requested_authn_context_returns_authn_context_if_claim_is_present()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim("saml:acr", "urn:oasis:names:tc:SAML:2.0:ac:classes:X509")
        ], "Test"));

        await Fixture.Client.GetAsync("/__signin", _ct);

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml());

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.AuthnStatement?.AuthnContextClassRef.ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:X509");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task auth_n_request_with_multiple_authn_context_class_refs_parsed_correctly()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());

        await Fixture.InitializeAsync();

        var requestedAuthnContext = """
            <samlp:RequestedAuthnContext Comparison="better">
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport</saml:AuthnContextClassRef>
                <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:X509</saml:AuthnContextClassRef>
            </samlp:RequestedAuthnContext>
            """;

        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(requestedAuthnContext: requestedAuthnContext));

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.Found);

        var parsedRequestedAuthnContext = await Fixture.NonRedirectingClient.GetFromJsonAsync<RequestedAuthnContext>("/__authentication-request", _ct);

        // Assert
        parsedRequestedAuthnContext.ShouldNotBeNull();
        parsedRequestedAuthnContext.Comparison.ShouldBe(AuthnContextComparison.Better);
        parsedRequestedAuthnContext.AuthnContextClassRefs.Count.ShouldBe(3);
        parsedRequestedAuthnContext.AuthnContextClassRefs.ShouldBe(["urn:oasis:names:tc:SAML:2.0:ac:classes:Password", "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport", "urn:oasis:names:tc:SAML:2.0:ac:classes:X509"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_name_id_format_is_supported()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml(nameIdFormat: SamlConstants.NameIdentifierFormats.Persistent);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_name_id_format_is_not_supported()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var unsupportedFormat = "urn:oasis:names:tc:SAML:2.0:nameid-format:kerberos";
        var authnRequestXml = Build.AuthNRequestXml(nameIdFormat: unsupportedFormat);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var errorResponse = await ExtractSamlErrorFromPostAsync(result);
        errorResponse.StatusCode.ShouldBe(SamlStatusCodes.Responder);
        errorResponse.SubStatusCode.ShouldBe(SamlStatusCodes.InvalidNameIdPolicy);
        errorResponse.StatusMessage.ShouldBe($"Requested NameID format '{unsupportedFormat}' is not supported by this IdP");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_name_id_policy_element_present_without_format()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // NameIDPolicy with SPNameQualifier but no Format - should succeed
        var authnRequestXml = Build.AuthNRequestXml(spNameQualifier: "https://custom.sp.com");
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_no_name_id_policy_element()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task response_uses_requested_name_id_format_from_policy()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id"), new Claim(JwtClaimTypes.Email, "test@test.com")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(nameIdFormat: SamlConstants.NameIdentifierFormats.EmailAddress);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("test@test.com");
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task response_uses_sp_default_name_id_format_when_no_policy()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var persistentIdentifier = Guid.NewGuid().ToString();
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id"), new Claim(ClaimTypes.NameIdentifier, persistentIdentifier)], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(); // No NameIDPolicy
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe(persistentIdentifier);
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Persistent);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task response_uses_email_format_when_no_policy_and_no_sp_default()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = null;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(JwtClaimTypes.Subject, "user-id"),
                new Claim(JwtClaimTypes.Email, "user@example.com")
            ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(); // No NameIDPolicy
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Unspecified);
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task transient_format_generates_different_ids_per_request()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(
            nameIdFormat: SamlConstants.NameIdentifierFormats.Transient);
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // First request
        var result1 = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);
        var response1 = await ExtractSamlSuccessFromPostAsync(result1, _ct);
        var nameId1 = response1.Assertion.Subject?.NameId;

        // Second request with same parameters
        var result2 = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);
        var response2 = await ExtractSamlSuccessFromPostAsync(result2, _ct);
        var nameId2 = response2.Assertion.Subject?.NameId;

        // Verify both responses succeeded
        response1.StatusCode.ShouldBe(SamlStatusCodes.Success);
        response2.StatusCode.ShouldBe(SamlStatusCodes.Success);

        // Verify format is transient
        response1.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Transient);
        response2.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Transient);

        // Verify IDs are different (transient should be unique per request)
        nameId1.ShouldNotBeNull();
        nameId2.ShouldNotBeNull();
        nameId1.ShouldNotBe(nameId2, "Transient NameIDs should be unique per authentication");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task persistent_format_uses_default_claim_type_from_service_provider_options()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var persistentId = "persistent-id-12345";
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(ClaimTypes.NameIdentifier, persistentId) // Default claim type
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml(); // No NameIDPolicy, uses SP default
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe(persistentId);
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Persistent);
        successResponse.Assertion.Subject?.SPNameQualifier.ShouldBe(Data.EntityId.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task persistent_format_uses_sp_specific_claim_type_override()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        sp.DefaultPersistentNameIdentifierClaimType = "custom_persistent_id";
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var spSpecificId = "sp-specific-id-67890";
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(ClaimTypes.NameIdentifier, "default-id"),
            new Claim("custom_persistent_id", spSpecificId) // SP-specific claim
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe(spSpecificId); // Uses SP override, not default
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Persistent);
        successResponse.Assertion.Subject?.SPNameQualifier.ShouldBe(Data.EntityId.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task persistent_format_fails_when_claim_missing()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // User WITHOUT ClaimTypes.NameIdentifier claim
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(JwtClaimTypes.Email, "user@example.com")
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert - if configured claim type cannot be found the request cannot be fulfilled
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task persistent_format_fails_when_claim_value_is_empty()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // User with empty ClaimTypes.NameIdentifier
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(ClaimTypes.NameIdentifier, "") // Empty claim
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert - if configured claim type cannot be found the request cannot be fulfilled
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task persistent_format_sets_sp_name_qualifier_to_sp_entity_id()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var persistentId = "persistent-abc123";
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(ClaimTypes.NameIdentifier, persistentId)
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.SPNameQualifier.ShouldBe(Data.EntityId.ToString());
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Persistent);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task persistent_format_works_with_custom_global_claim_type()
    {
        // Arrange
        Fixture.ConfigureSamlOptions = options =>
        {
            options.DefaultPersistentNameIdentifierClaimType = "app_persistent_id";
        };

        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var customPersistentId = "global-custom-id-xyz";
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim("app_persistent_id", customPersistentId), // Custom global claim type
            // Note: ClaimTypes.NameIdentifier NOT present
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe(customPersistentId);
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.Persistent);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multiple_users_get_different_persistent_ids_same_sp()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Persistent;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // User A
        var userAPersistentId = "user-a-persistent-123";
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-a"),
            new Claim(ClaimTypes.NameIdentifier, userAPersistentId)
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var resultA = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);
        var responseA = await ExtractSamlSuccessFromPostAsync(resultA, _ct);

        // User B (re-authenticate as different user)
        var userBPersistentId = "user-b-persistent-456";
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-b"),
            new Claim(ClaimTypes.NameIdentifier, userBPersistentId)
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var resultB = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);
        var responseB = await ExtractSamlSuccessFromPostAsync(resultB, _ct);

        // Assert
        responseA.StatusCode.ShouldBe(SamlStatusCodes.Success);
        responseB.StatusCode.ShouldBe(SamlStatusCodes.Success);

        responseA.Assertion.Subject?.NameId.ShouldBe(userAPersistentId);
        responseB.Assertion.Subject?.NameId.ShouldBe(userBPersistentId);

        // Verify IDs are distinct
        responseA.Assertion.Subject?.NameId.ShouldNotBe(responseB.Assertion.Subject?.NameId,
            "Different users should have different persistent identifiers");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task email_format_fails_when_claim_missing()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.EmailAddress;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // User WITHOUT ClaimTypes.NameIdentifier claim
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id")
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert - if configured claim type cannot be found the request cannot be fulfilled
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task email_format_fails_when_claim_value_is_empty()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.EmailAddress;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // User with empty ClaimTypes.NameIdentifier
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(ClaimTypes.Email, "") // Empty claim
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert - if configured claim type cannot be found the request cannot be fulfilled
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task email_format_returns_expected_value_when_claim_is_present()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.EmailAddress;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // User with empty ClaimTypes.NameIdentifier
        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(ClaimTypes.Email, "test@testing.com")
        ], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        successResponse.Assertion.Subject?.NameId.ShouldBe("test@testing.com");
        successResponse.Assertion.Subject?.NameIdFormat.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }
}

