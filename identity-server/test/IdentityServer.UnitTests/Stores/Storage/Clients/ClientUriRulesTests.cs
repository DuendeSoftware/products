// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Stores.Storage.Clients;

namespace UnitTests.Stores.Storage.Clients;

public class ClientUriRulesTests
{
    private static readonly IReadOnlyList<string> DefaultDenyList = [.. new ValidationOptions().InvalidRedirectUriPrefixes];

    // === Matrix: values valid for redirect, web URL and back-channel profiles ===

    [Theory]
    [InlineData("https://app.example.com/cb")]
    [InlineData("http://localhost:5000/cb")]
    [InlineData("http://127.0.0.1:7890/cb")]
    [InlineData("http://127.1.2.3/cb")]
    [InlineData("http://[::1]:7890/cb")]
    public void value_is_valid_for_redirect_web_and_backchannel(string value)
    {
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldBeNull();
    }

    // === Matrix: origins (no path/query/trailing-slash/userinfo) valid across all four profiles ===

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1:7890")]
    [InlineData("http://127.1.2.3")]
    [InlineData("http://[::1]:7890")]
    public void value_is_valid_for_redirect_web_backchannel_and_cors(string value) => AssertAllValid(value);

    private static void AssertAllValid(string value)
    {
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldBeNull();
    }

    [Fact]
    public void https_cors_origin_is_valid() => ClientUriRules.ValidateCorsOrigin("https://app.example.com").ShouldBeNull();

    // === Custom scheme: valid as a redirect URI, rejected elsewhere ===

    [Fact]
    public void custom_scheme_is_valid_for_redirect_but_rejected_elsewhere()
    {
        const string value = "com.example.app:/cb";

        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldNotBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    // === Non-loopback http: rejected everywhere except back-channel (D1) ===

    [Theory]
    [InlineData("http://app.example.com/cb")]
    [InlineData("HTTP://APP.EXAMPLE.COM/cb")]
    [InlineData("http://localhost.evil.com/cb")] // not loopback: a subdomain of "localhost.evil.com", not "localhost"
    public void non_loopback_http_is_rejected_except_for_back_channel(string value)
    {
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    // === Values rejected across all four profiles ===

    [Fact]
    public void relative_value_is_rejected_everywhere()
    {
        const string value = "cb";

        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldNotBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    [Fact]
    public void leading_slash_value_is_rejected_everywhere()
    {
        // On Windows "/cb" is "not an absolute URI"; on Linux it's parsed as "file:///cb" and
        // rejected by the IsFile check. Assert rejection only, not the specific reason.
        const string value = "/cb";

        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldNotBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    [Fact]
    public void file_uri_is_rejected_everywhere()
    {
        const string value = "file:///etc/passwd";

        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldNotBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("https://app.example.com/cb#frag")]
    [InlineData("https://app.example.com/cb#")]
    public void fragment_is_rejected_everywhere(string value)
    {
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldNotBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("JAVASCRIPT:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("wss://x")]
    [InlineData("view-source:https://x")]
    public void denied_scheme_is_rejected_everywhere(string value)
    {
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldNotBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    [Fact]
    public void wildcard_host_is_rejected_everywhere()
    {
        const string value = "https://*.example.com/cb";

        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldNotBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldNotBeNull();
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();
    }

    // === Host with underscores still parses ===

    [Fact]
    public void host_with_underscore_is_valid_for_redirect_and_web_and_backchannel()
    {
        const string value = "https://client_one/logout";

        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldBeNull();
        ClientUriRules.ValidateWebUrl(value).ShouldBeNull();
        ClientUriRules.ValidateBackChannelUri(value).ShouldBeNull();
    }

    // === Deny-list behavior ===

    [Theory]
    [InlineData("custom:/cb")]
    [InlineData("CUSTOM:/cb")]
    public void custom_deny_list_prefix_rejects_matching_values(string value)
    {
        IReadOnlyList<string> denyList = ["custom:"];

        ClientUriRules.ValidateRedirectUri(value, denyList).ShouldNotBeNull();
    }

    [Fact]
    public void empty_deny_list_treats_javascript_scheme_as_custom_scheme_and_passes_redirect_profile()
    {
        // Expected: with no deny-list configured, ClientUriRules has no other way to reject an
        // unusual scheme like "javascript:" on the redirect profile — the deny-list is the only
        // control for schemes other than http/https. This mirrors production default behavior
        // only when InvalidRedirectUriPrefixes is cleared, which is not the shipped default.
        IReadOnlyList<string> emptyDenyList = [];

        ClientUriRules.ValidateRedirectUri("javascript:alert(1)", emptyDenyList).ShouldBeNull();
    }

    // === CORS origin shape: must be an origin, not a URL ===

    [Theory]
    [InlineData("https://site.test/path")]
    [InlineData("https://site.test/")]
    [InlineData("https://site.test?x=1")]
    [InlineData("https://u@site.test")]
    [InlineData("https://site.test/.")]
    [InlineData("https://site.test/a/..")]
    [InlineData("https://@site.test")]
    [InlineData("https://site.test:443")]
    [InlineData("http://localhost:80")]
    [InlineData("https://bücher.example")]
    public void cors_origin_that_is_not_canonical_is_rejected(string value) =>
        ClientUriRules.ValidateCorsOrigin(value).ShouldNotBeNull();

    [Theory]
    [InlineData("https://site.test")]
    [InlineData("https://site.test:8443")]
    [InlineData("http://localhost:3000")]
    [InlineData("https://xn--bcher-kva.example")]
    [InlineData("http://[::1]:3000")]
    [InlineData("HTTPS://SITE.TEST")]
    public void cors_origin_in_canonical_form_is_accepted(string value) =>
        ClientUriRules.ValidateCorsOrigin(value).ShouldBeNull();

    // === Whitespace and control characters are rejected before parsing ===

    [Theory]
    [InlineData("\thttps://example.com/cb")]
    [InlineData("https://example.com/cb\n")]
    [InlineData("https://exa mple.com/cb")]
    [InlineData(" javascript:alert(1)")]
    public void value_with_whitespace_is_rejected_for_redirect(string value) =>
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();

    [Fact]
    public void value_with_whitespace_is_rejected_for_web_url() =>
        ClientUriRules.ValidateWebUrl("https://exa mple.com/cb").ShouldNotBeNull();

    // === Explicit loopback hosts only ===

    [Theory]
    [InlineData("http://[::ffff:127.0.0.1]/cb")]
    [InlineData("http://2130706433/cb")]
    [InlineData("http://0x7f000001/cb")]
    [InlineData("http://127.1/cb")]
    [InlineData("http://127.0.0.010/cb")]
    [InlineData("http://localhost@evil.com/cb")]
    public void odd_loopback_forms_are_rejected_for_redirect(string value) =>
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldNotBeNull();

    [Theory]
    [InlineData("http://127.0.0.1:5000/cb")]
    [InlineData("http://127.1.2.3/cb")]
    [InlineData("http://LOCALHOST/cb")]
    [InlineData("http://[::1]:5000/cb")]
    public void explicit_loopback_forms_are_accepted_for_redirect(string value) =>
        ClientUriRules.ValidateRedirectUri(value, DefaultDenyList).ShouldBeNull();

    // === HaveSameOrigin ===

    [Fact]
    public void have_same_origin_true_for_same_scheme_host_and_default_port()
    {
        var a = new Uri("https://a.example.com/x");
        var b = new Uri("https://a.example.com:443/y");

        ClientUriRules.HaveSameOrigin(a, b).ShouldBeTrue();
    }

    [Fact]
    public void have_same_origin_false_for_different_scheme()
    {
        var a = new Uri("https://a.example.com/x");
        var b = new Uri("http://a.example.com/y");

        ClientUriRules.HaveSameOrigin(a, b).ShouldBeFalse();
    }

    [Fact]
    public void have_same_origin_false_for_different_port()
    {
        var a = new Uri("https://a.example.com/x");
        var b = new Uri("https://a.example.com:8443/y");

        ClientUriRules.HaveSameOrigin(a, b).ShouldBeFalse();
    }

    [Fact]
    public void have_same_origin_is_host_case_insensitive()
    {
        var a = new Uri("https://A.Example.com/x");
        var b = new Uri("https://a.example.com/y");

        ClientUriRules.HaveSameOrigin(a, b).ShouldBeTrue();
    }

    [Fact]
    public void have_same_origin_true_for_unicode_and_punycode_host()
    {
        var a = new Uri("https://bücher.example/logout");
        var b = new Uri("https://xn--bcher-kva.example/cb");

        ClientUriRules.HaveSameOrigin(a, b).ShouldBeTrue();
    }
}
