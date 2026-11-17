// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlXmlNestingDepthTests : IAsyncDisposable
{
    private const string Category = "SAML XML nesting depth";
    private const string ErrorPath = "/saml-error";
    private const int DefaultMaxDepth = 32;
    private const string SigninError = "The SAML request could not be processed";
    private const string LogoutError = "The SAML logout request could not be processed";

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly SamlFixture _fixture = new();
    private readonly ExceptionRecorder _recorder = new();

    public SamlXmlNestingDepthTests()
    {
        _fixture.ConfigureIdentityServerOptions = options => options.UserInteraction.ErrorUrl = ErrorPath;
        _fixture.ConfigureServices = services => services.AddSingleton<IStartupFilter>(_recorder);

        var sp = _fixture.Builder.SamlServiceProvider();
        sp.AssertionConsumerServiceUrls =
            [new IndexedEndpoint { Location = _fixture.Data.AcsUrl.OriginalString, Binding = SamlBinding.HttpPost }];
        _fixture.ServiceProviders.Add(sp);
    }

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signin_redirect_exceeds_default_nesting_depth()
    {
        await _fixture.InitializeAsync();

        using var response = await SendSigninRedirectAsync(AuthnRequestNestedTo(DefaultMaxDepth + 1));

        await AssertErrorRedirectAsync(response, SigninError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signin_post_exceeds_default_nesting_depth()
    {
        await _fixture.InitializeAsync();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SAMLRequest"] = SamlTestHelpers.ConvertToBase64Encoded(AuthnRequestNestedTo(DefaultMaxDepth + 1))
        });
        using var response = await _fixture.NonRedirectingClient.PostAsync("/Saml2/SSO", content, _ct);

        await AssertErrorRedirectAsync(response, SigninError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_logout_response_status_codes_exceed_default_nesting_depth()
    {
        await _fixture.InitializeAsync();

        var encoded = await SamlTestHelpers.EncodeRequest(LogoutResponseNestedTo(DefaultMaxDepth + 1), _ct);
        using var response = await _fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO?SAMLResponse={encoded}", _ct);

        await AssertErrorRedirectAsync(response, LogoutError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_logout_request_post_exceeds_default_nesting_depth()
    {
        await _fixture.InitializeAsync();

        var xml = _fixture.Builder.LogoutRequestXml(extensions: SamlTestHelpers.                ExtensionsNestedTo(DefaultMaxDepth + 1));
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SAMLRequest"] = SamlTestHelpers.ConvertToBase64Encoded(xml)
        });
        using var response = await _fixture.NonRedirectingClient.PostAsync("/Saml2/SLO", content, _ct);

        await AssertErrorRedirectAsync(response, LogoutError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task success_when_signin_is_nested_to_configured_depth_with_text_in_deepest_element()
    {
        const int configuredDepth = 8;
        _fixture.ConfigureSamlOptions = options => options.MaxXmlNestingDepth = configuredDepth;
        await _fixture.InitializeAsync();

        using var response = await SendSigninRedirectAsync(AuthnRequestNestedTo(configuredDepth));

        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther, _recorder.Exception?.ToString());
        var location = response.Headers.Location.ShouldNotBeNull();
        new Uri(new Uri(_fixture.Url()), location).AbsolutePath.ShouldBe(_fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signin_exceeds_configured_nesting_depth()
    {
        const int configuredDepth = 8;
        _fixture.ConfigureSamlOptions = options => options.MaxXmlNestingDepth = configuredDepth;
        await _fixture.InitializeAsync();

        using var response = await SendSigninRedirectAsync(AuthnRequestNestedTo(configuredDepth + 1));

        await AssertErrorRedirectAsync(response, SigninError);
    }

    private async Task<HttpResponseMessage> SendSigninRedirectAsync(string xml)
    {
        var encoded = await SamlTestHelpers.EncodeRequest(xml, _ct);
        return await _fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO?SAMLRequest={encoded}", _ct);
    }

    private async Task AssertErrorRedirectAsync(HttpResponseMessage response, string expectedError)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect, _recorder.Exception?.ToString());
        _recorder.Exception.ShouldBeNull();
        var location = response.Headers.Location.ShouldNotBeNull();
        new Uri(new Uri(_fixture.Url()), location).AbsolutePath.ShouldBe(ErrorPath);
        var errorMessage = await _fixture.GetErrorFromRedirect(response, _ct);
        errorMessage.ShouldBe(expectedError);
    }

    // The IdP ignores Extensions content; nested markup here is otherwise accepted.
    private string AuthnRequestNestedTo(int deepestElementDepth) =>
        _fixture.Builder.AuthNRequestXml(extensions: SamlTestHelpers.ExtensionsNestedTo(deepestElementDepth));

    // LogoutResponse is depth 0 and Status is depth 1, so StatusCode elements start at depth 2.
    private string LogoutResponseNestedTo(int deepestElementDepth)
    {
        const string statusCode = """<samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success">""";
        var status = $"<samlp:Status>{SamlTestHelpers.Nested(deepestElementDepth - 1, statusCode, statusCode, "</samlp:StatusCode>", "")}</samlp:Status>";

        return $"""
                <samlp:LogoutResponse ID="_{Guid.NewGuid():N}" Version="2.0"
                    IssueInstant="{_fixture.Now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}" InResponseTo="_request"
                    xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                    xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
                    <saml:Issuer>{_fixture.Data.EntityId}</saml:Issuer>
                    {status}
                </samlp:LogoutResponse>
                """;
    }
}
