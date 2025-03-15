// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class OidcReturnUrlParserTests
{
    private OidcReturnUrlParser _subject;

    private IdentityServerOptions _options = new IdentityServerOptions();
    private MockServerUrls _urls = new MockServerUrls();

    public OidcReturnUrlParserTests()
    {
        _urls.Origin = "https://server";

        _subject = new OidcReturnUrlParser(
            _options,
            null, null,
            _urls,
            new LoggerFactory().CreateLogger<OidcReturnUrlParser>());
    }

    [Theory]
    [InlineData("/connect/authorize")]
    [InlineData("/connect/authorize?foo=f1&bar=b1")]
    [InlineData("/connect/authorize/callback")]
    [InlineData("/connect/authorize/callback?foo=f1&bar=b1")]
    [InlineData("/foo/connect/authorize")]
    [InlineData("/foo/connect/authorize/callback")]
    public void IsValidReturnUrl_accepts_authorize_or_authorizecallback(string url)
    {
        var valid = _subject.IsValidReturnUrl(url);
        valid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(default(string))]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/path")]
    [InlineData("//connect/authorize")]
    [InlineData("/connect/authorizex")]
    [InlineData("/connect")]
    [InlineData("/connect/token")]
    [InlineData("/authorize")]
    [InlineData("/foo?/connect/authorize")]
    [InlineData("/foo#/connect/authorize")]
    [InlineData("/foo?#/connect/authorize")]
    [InlineData("/foo#?/connect/authorize")]
    [InlineData("//server/connect/authorize")]
    public void IsValidReturnUrl_rejects_non_authorize_or_authorizecallback(string url)
    {
        var valid = _subject.IsValidReturnUrl(url);
        valid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("https://server/connect/authorize")]
    [InlineData("HTTPS://server/connect/authorize")]
    [InlineData("https://SERVER/connect/authorize")]
    [InlineData("https://server/foo/connect/authorize")]
    public void IsValidReturnUrl_accepts_urls_with_current_host(string url)
    {
        _options.UserInteraction.AllowOriginInReturnUrl = true;
        var valid = _subject.IsValidReturnUrl(url);
        valid.ShouldBeTrue();
    }

    [Fact]
    public void IsValidReturnUrl_when_AllowHostInReturnUrl_disabled_rejects_urls_with_current_host()
    {
        _options.UserInteraction.AllowOriginInReturnUrl = false;
        var valid = _subject.IsValidReturnUrl("https://server/connect/authorize");
        valid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("http://server/connect/authorize")]
    [InlineData("https:\\/server/connect/authorize")]
    [InlineData("https:\\\\server/connect/authorize")]
    [InlineData("https://foo/connect/authorize")]
    [InlineData("https://serverhttps://server/connect/authorize")]
    [InlineData("https://serverfoo/connect/authorize")]
    [InlineData("https://server//foo/connect/authorize")]
    [InlineData("https://server:443/connect/authorize")]
    public void IsValidReturnUrl_rejects_urls_with_incorrect_current_host(string url)
    {
        _options.UserInteraction.AllowOriginInReturnUrl = true;
        var valid = _subject.IsValidReturnUrl(url);
        valid.ShouldBeFalse();
    }


    [Fact]
    public void IsValidReturnUrl_accepts_urls_with_unicode()
    {
        _options.UserInteraction.AllowOriginInReturnUrl = true;
        _urls.Origin = "https://" + new HostString("грант.рф").ToUriComponent();

        var valid = _subject.IsValidReturnUrl("https://xn--80af5akm.xn--p1ai/connect/authorize");
        valid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://server:443/connect/authorize")]
    [InlineData("HTTPS://server:443/connect/authorize")]
    [InlineData("https://SERVER:443/connect/authorize")]
    public void IsValidReturnUrl_accepts_urls_with_current_port(string url)
    {
        _options.UserInteraction.AllowOriginInReturnUrl = true;
        _urls.Origin = "https://server:443";

        var valid = _subject.IsValidReturnUrl(url);
        valid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://server/connect/authorize")]
    [InlineData("https://server:80/connect/authorize")]
    [InlineData("https://server:4/connect/authorize")]
    [InlineData("https://foo:443/connect/authorize")]
    [InlineData("https://server:4433/connect/authorize")]
    [InlineData("https://server:443https://server:443/connect/authorize")]
    [InlineData("https://serverfoo:443/connect/authorize")]
    [InlineData("https://server:443foo/connect/authorize")]
    [InlineData("https://server:443//foo/connect/authorize")]
    public void IsValidReturnUrl_rejects_urls_with_incorrect_current_port(string url)
    {
        _options.UserInteraction.AllowOriginInReturnUrl = true;
        _urls.Origin = "https://server:443";

        var valid = _subject.IsValidReturnUrl(url);
        valid.ShouldBeFalse();
    }
}
