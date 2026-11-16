// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Cryptography.Xml;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

/// <summary>
/// Verifies that a text-only rejection during redirect unbinding reaches the error page rather
/// than escaping as an unhandled exception. The nested Issuer would otherwise flatten to a valid
/// entity id and fail later with the same generic error, so these tests cover error routing. The
/// text-only rule itself is covered by <c>HttpRedirectBindingTests</c>.
/// </summary>
public class SamlRedirectTextContentTests : IAsyncLifetime
{
    private const string Category = "SAML redirect text content";
    private const string ErrorPath = "/saml-error";

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly SamlFixture _fixture = new();
    private readonly ExceptionRecorder _recorder = new();

    public SamlRedirectTextContentTests()
    {
        _fixture.ConfigureIdentityServerOptions = options => options.UserInteraction.ErrorUrl = ErrorPath;
        _fixture.ConfigureServices = services => services.AddSingleton<IStartupFilter>(_recorder);
    }

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", Category)]
    public async Task signin_redirect_with_nested_issuer_redirects_to_error_page()
    {
        var xml = _fixture.Builder.AuthNRequestXml(issuer: $"<nested>{_fixture.Data.EntityId}</nested>");

        await AssertErrorRedirectAsync("/Saml2/SSO", xml, "The SAML request could not be processed");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_redirect_with_nested_issuer_redirects_to_error_page()
    {
        var xml = _fixture.Builder.LogoutRequestXml(issuer: $"<nested>{_fixture.Data.EntityId}</nested>");

        await AssertErrorRedirectAsync("/Saml2/SLO", xml, "The SAML logout request could not be processed");
    }

    private async Task AssertErrorRedirectAsync(string path, string xml, string expectedError)
    {
        var encoded = await SamlTestHelpers.EncodeRequest(xml, _ct);
        var sigAlg = Uri.EscapeDataString(SignedXml.XmlDsigRSASHA256Url);

        using var response = await _fixture.NonRedirectingClient.GetAsync(
            $"{path}?SAMLRequest={encoded}&SigAlg={sigAlg}&Signature=AA%3D%3D", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect, _recorder.Exception?.ToString());
        _recorder.Exception.ShouldBeNull();
        var location = response.Headers.Location.ShouldNotBeNull();
        new Uri(new Uri(_fixture.Url()), location).AbsolutePath.ShouldBe(ErrorPath);
        var errorMessage = await _fixture.GetErrorFromRedirect(response, _ct);
        errorMessage.ShouldBe(expectedError);
    }
}
