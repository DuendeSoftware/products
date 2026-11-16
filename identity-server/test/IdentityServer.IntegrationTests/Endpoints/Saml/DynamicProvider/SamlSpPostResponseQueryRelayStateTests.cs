// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Text.RegularExpressions;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

/// <summary>
/// Checks a Copilot review claim on <see cref="Internal.Saml.Sp.Commands.HttpRequestData" />
/// (constructed around line 129): that an SP-initiated AuthnResponse delivered on the HTTP-POST
/// binding, with a bare, valueless "RelayState" query string parameter appended to the ACS URL
/// (no '='), would no longer fall back to the POSTed form's RelayState value, losing the
/// stored-request-state cookie lookup (InResponseTo / request id) and failing sign-in.
/// </summary>
public class SamlSpPostResponseQueryRelayStateTests(ITestOutputHelper output)
{
    private const string Category = "SAML SP POST binding RelayState";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task control_sp_initiated_post_response_without_query_suffix_completes_sign_in()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = new SamlDynamicProviderFixture(output,
            additionalSpServices: services => services.AddSingleton<IStartupFilter>(recorder));
        await fixture.InitializeAsync();

        var clientUri = fixture.ClientHost!.Uri();

        var (actionUrl, formData) = await GetAcsPostFormAsync(fixture, clientUri);

        using var formContent = new FormUrlEncodedContent(formData);
        var acsResponse = await fixture.BrowserClient!.PostAsync(actionUrl, formContent, _ct);
        acsResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
            recorder.Exception?.ToString() ?? "no exception recorded");

        // The ACS POST itself returns another HTML auto-post form (the OIDC code/id_token
        // "form_post" callback to the client app), not the final page; follow it the same way
        // FollowRedirectChainAsync does for the rest of the chain.
        var finalResponse = await SubmitFurtherHtmlFormsAsync(fixture, acsResponse);

        finalResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
            recorder.Exception?.ToString() ?? "no exception recorded");
        var body = await finalResponse.Content.ReadAsStringAsync(_ct);
        body.ShouldContain(SamlDynamicProviderFixture.TestUserSub);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sp_initiated_post_response_with_bare_relay_state_query_suffix_still_completes_sign_in()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = new SamlDynamicProviderFixture(output,
            additionalSpServices: services => services.AddSingleton<IStartupFilter>(recorder));
        await fixture.InitializeAsync();

        var clientUri = fixture.ClientHost!.Uri();

        var (actionUrl, formData) = await GetAcsPostFormAsync(fixture, clientUri);

        // Append a bare, valueless "RelayState" query string parameter (no '=') to the ACS
        // URL, alongside the normal POSTed form fields (SAMLResponse + RelayState). Only the
        // HTTP-Redirect binding rejects duplicate SAML parameters, so this bare query parameter
        // must not change how this POST is handled.
        var separator = actionUrl.Contains('?') ? "&" : "?";
        var acsUrlWithBareQueryRelayState = $"{actionUrl}{separator}RelayState";

        using var formContent = new FormUrlEncodedContent(formData);
        var acsResponse = await fixture.BrowserClient!.PostAsync(acsUrlWithBareQueryRelayState, formContent, _ct);
        acsResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
            recorder.Exception?.ToString() ?? "no exception recorded");

        var finalResponse = await SubmitFurtherHtmlFormsAsync(fixture, acsResponse);

        finalResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
            recorder.Exception?.ToString() ?? "no exception recorded");
        var body = await finalResponse.Content.ReadAsStringAsync(_ct);
        body.ShouldContain(SamlDynamicProviderFixture.TestUserSub);
    }

    /// <summary>
    /// Drives the SP-initiated challenge (client → SP account/login → IdP SSO → auto-login →
    /// back to the IdP) and stops right before the final HTML auto-post form is submitted to
    /// the SP's ACS endpoint, so the test can modify the POST's target URL. This exercises an
    /// SP-initiated flow: the SP's AuthnRequest is sent on the redirect binding, which stores a
    /// stored-request-state cookie keyed by RelayState and causes the returned AuthnResponse to
    /// carry InResponseTo.
    /// </summary>
    private async Task<(string actionUrl, Dictionary<string, string> formData)> GetAcsPostFormAsync(
        SamlDynamicProviderFixture fixture, string clientUri)
    {
        // AllowAutoRedirect is enabled on fixture.BrowserClient, so this single GET follows the
        // entire 30x redirect chain (OIDC challenge, SP SAML challenge, IdP SSO redirect binding,
        // auto-login) and stops at the first 200 OK response, which is the IdP's HTML page that
        // auto-posts the SAMLResponse to the SP's ACS endpoint.
        var response = await fixture.BrowserClient!.GetAsync($"{clientUri}/protected", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "expected the IdP's auto-post HTML page");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        var html = await response.Content.ReadAsStringAsync(_ct);

        var actionMatch = Regex.Match(html, @"<form[^>]+action=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        actionMatch.Success.ShouldBeTrue("expected an auto-post <form> in the IdP's response");
        var actionUrl = System.Web.HttpUtility.HtmlDecode(actionMatch.Groups[1].Value);
        actionUrl.ShouldContain("/Saml2/Acs");

        var formData = new Dictionary<string, string>();
        var inputTagMatches = Regex.Matches(html, @"<input\b[^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match inputTag in inputTagMatches)
        {
            var tag = inputTag.Value;

            var typeMatch = Regex.Match(tag, @"\btype=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            if (!typeMatch.Success || !typeMatch.Groups[1].Value.Equals("hidden", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nameMatch = Regex.Match(tag, @"\bname=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            var valueMatch = Regex.Match(tag, @"\bvalue=['""]([^'""]*)['""]", RegexOptions.IgnoreCase);
            if (!nameMatch.Success)
            {
                continue;
            }

            var name = nameMatch.Groups[1].Value;
            var value = valueMatch.Success ? System.Web.HttpUtility.HtmlDecode(valueMatch.Groups[1].Value) : string.Empty;

            if (name.Equals("SAMLResponse", StringComparison.OrdinalIgnoreCase))
            {
                var decodedBytes = Convert.FromBase64String(value);
                var xml = System.Text.Encoding.UTF8.GetString(decodedBytes);
                value = SamlTestHelpers.ConvertToBase64Encoded(xml);
            }

            formData[name] = value;
        }

        formData.ShouldContainKey("SAMLResponse");
        formData.ShouldContainKey("RelayState");

        return (actionUrl, formData);
    }

    /// <summary>
    /// Follows any further HTML auto-submit forms (e.g. the OIDC "form_post" response_mode
    /// callback from the SP back to the client app) after the ACS POST, mirroring what
    /// <see cref="SamlDynamicProviderFixture.FollowRedirectChainAsync" /> does for the rest of
    /// the chain. Generic (no SAML-specific field re-encoding) since this is not a SAML message.
    /// </summary>
    private async Task<HttpResponseMessage> SubmitFurtherHtmlFormsAsync(
        SamlDynamicProviderFixture fixture, HttpResponseMessage response)
    {
        for (var i = 0; i < 5; i++)
        {
            if (response.StatusCode != HttpStatusCode.OK ||
                response.Content.Headers.ContentType?.MediaType != "text/html")
            {
                break;
            }

            var html = await response.Content.ReadAsStringAsync(_ct);
            var actionMatch = Regex.Match(html, @"<form[^>]+action=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            if (!actionMatch.Success)
            {
                break;
            }

            var actionUrl = System.Web.HttpUtility.HtmlDecode(actionMatch.Groups[1].Value);
            var formData = new Dictionary<string, string>();
            var inputTagMatches = Regex.Matches(html, @"<input\b[^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match inputTag in inputTagMatches)
            {
                var tag = inputTag.Value;
                var typeMatch = Regex.Match(tag, @"\btype=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (!typeMatch.Success || !typeMatch.Groups[1].Value.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var nameMatch = Regex.Match(tag, @"\bname=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                var valueMatch = Regex.Match(tag, @"\bvalue=['""]([^'""]*)['""]", RegexOptions.IgnoreCase);
                if (!nameMatch.Success)
                {
                    continue;
                }

                var name = nameMatch.Groups[1].Value;
                var value = valueMatch.Success ? System.Web.HttpUtility.HtmlDecode(valueMatch.Groups[1].Value) : string.Empty;
                formData[name] = value;
            }

            if (formData.Count == 0)
            {
                break;
            }

            using var formContent = new FormUrlEncodedContent(formData);
            response = await fixture.BrowserClient!.PostAsync(actionUrl, formContent, _ct);
        }

        return response;
    }
}
