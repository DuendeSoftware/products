// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Net;
using Duende.IdentityServer.Services.Default;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests.Services.Default;

public class DefaultUiLocalesServiceTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly DefaultHttpContext _httpContext;
    private readonly HttpContextAccessor _httpContextAccessor;
    private readonly RequestLocalizationOptions _requestLocalizationOptions;
    private readonly DefaultUiLocalesService _subject;

    public DefaultUiLocalesServiceTests()
    {
        _httpContext = new DefaultHttpContext();
        _httpContextAccessor = new HttpContextAccessor { HttpContext = _httpContext };
        _requestLocalizationOptions = new RequestLocalizationOptions();
        _subject = new DefaultUiLocalesService(_httpContextAccessor, Options.Create(_requestLocalizationOptions),
            NullLogger<DefaultUiLocalesService>.Instance);
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_HttpContextIsNull_DoesNothing()
    {
        _httpContextAccessor.HttpContext = null;

        await _subject.StoreUiLocalesForRedirectAsync("en-US", _ct);

        var setCookieHeader = _httpContext.Response.Headers.Where(x => x.Key == "Set-Cookie");
        setCookieHeader.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_NoCookieProvider_DoesNothing()
    {
        _requestLocalizationOptions.RequestCultureProviders.Clear();

        await _subject.StoreUiLocalesForRedirectAsync("en-US", _ct);

        var setCookieHeader = _httpContext.Response.Headers.Where(x => x.Key == "Set-Cookie");
        setCookieHeader.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_UnsupportedLocale_DoesNothing()
    {
        _requestLocalizationOptions.SupportedUICultures = new List<CultureInfo> { new("fr-FR") };

        await _subject.StoreUiLocalesForRedirectAsync("en-US", _ct);

        var setCookieHeader = _httpContext.Response.Headers.Where(x => x.Key == "Set-Cookie");
        setCookieHeader.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_MultipleSupportedLocalesWithNoMatch_DoesNothing()
    {
        _requestLocalizationOptions.SupportedUICultures = new List<CultureInfo> { new("fr-FR") };

        await _subject.StoreUiLocalesForRedirectAsync("en-US nb-NO", _ct);

        var setCookieHeader = _httpContext.Response.Headers.Where(x => x.Key == "Set-Cookie");
        setCookieHeader.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task StoreUiLocalesForRedirectAsync_NullOrWhitespaceUiLocales_DoesNothing(string? uiLocales)
    {
        await _subject.StoreUiLocalesForRedirectAsync(uiLocales, _ct);

        var setCookieHeader = _httpContext.Response.Headers.Where(x => x.Key == "Set-Cookie");
        setCookieHeader.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_NoSupportedCultures_DoesNothing()
    {
        _requestLocalizationOptions.SupportedUICultures = new List<CultureInfo>();

        await _subject.StoreUiLocalesForRedirectAsync("en-US", _ct);

        var setCookieHeader = _httpContext.Response.Headers.Where(x => x.Key == "Set-Cookie");
        setCookieHeader.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_SupportedLocale_SetsCookie()
    {
        var expectedSetCookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(new CultureInfo("en-US")));
        _requestLocalizationOptions.SupportedUICultures = new List<CultureInfo> { new("en-US") };

        await _subject.StoreUiLocalesForRedirectAsync("en-US", _ct);

        var cookieContainer = new CookieContainer();
        var cookies = _httpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));
        var cookie = cookieContainer.GetCookies(new Uri("http://server")).FirstOrDefault(x => x.Name.Equals(CookieRequestCultureProvider.DefaultCookieName, StringComparison.OrdinalIgnoreCase));
        cookie?.Value.ShouldBe(Uri.EscapeDataString(expectedSetCookieValue));
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_MultipleSupportedLocales_SetsFirstSupportedLocale()
    {
        var expectedSetCookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(new CultureInfo("en-US")));
        _requestLocalizationOptions.SupportedUICultures = new List<CultureInfo>
        {
            new("fr-FR"),
            new("en-US"),
            new("de-DE")
        };

        await _subject.StoreUiLocalesForRedirectAsync("en-US fr-FR", _ct);

        var cookieContainer = new CookieContainer();
        var cookies = _httpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));
        var cookie = cookieContainer.GetCookies(new Uri("http://server")).FirstOrDefault(x => x.Name.Equals(CookieRequestCultureProvider.DefaultCookieName, StringComparison.OrdinalIgnoreCase));
        cookie?.Value.ShouldBe(Uri.EscapeDataString(expectedSetCookieValue));
    }

    [Fact]
    public async Task StoreUiLocalesForRedirectAsync_MultipleSupportedLocalesWithMatchNotFirstInUiLocales_SetsFirstSupportedLocale()
    {
        var expectedSetCookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(new CultureInfo("en-US")));
        _requestLocalizationOptions.SupportedUICultures = new List<CultureInfo>
        {
            new("nb-NO"),
            new("en-US"),
            new("de-DE")
        };

        await _subject.StoreUiLocalesForRedirectAsync("fr-FR en-US", _ct);

        var cookieContainer = new CookieContainer();
        var cookies = _httpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));
        var cookie = cookieContainer.GetCookies(new Uri("http://server")).FirstOrDefault(x => x.Name.Equals(CookieRequestCultureProvider.DefaultCookieName, StringComparison.OrdinalIgnoreCase));
        cookie?.Value.ShouldBe(Uri.EscapeDataString(expectedSetCookieValue));
    }
}
