// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using System.Text.Json;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// An <see cref="HttpClient" /> configured as a browser (design doc D8): a cookie jar on the test clock, and automatic
/// redirects switched off. Each instance is one user agent. It reaches the environment's virtual hosts (for example
/// <c>https://sut</c>) without DNS.
/// </summary>
/// <remarks>
/// A scenario is written as steps through pages (D39): <see cref="OpenAsync" /> follows redirects on its own and stops
/// at the next page that needs input, returned as a typed <see cref="Page" />. The browser keeps a journal of every
/// request it sends, including those automatic hops, and <c>VerifyScenario</c> snapshots it.
/// </remarks>
public sealed class TestBrowser : HttpClient
{
    /// <summary>The response header a harness endpoint that isn't HTML uses to say which page it is.</summary>
    internal const string PageHeader = "X-OutsideIn-Page";

    private const int MaxHops = 25;

    // Fields that hold generated protocol values. Their values become numbered placeholders, such as {Code_1} (D26).
    private static readonly HashSet<string> ProtocolFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "state", "nonce", "code_challenge", "session_state", "sid", "jti", "request_uri",
        "id_token", "access_token", "refresh_token", "id_token_hint", "at_hash", "c_hash", "s_hash",
    };

    // Cookie names that end in a generated value, and the field name that value is registered under.
    private static readonly (string Prefix, string FieldName)[] GeneratedCookieNames =
    [
        (".AspNetCore.Correlation.", "correlation"),
        (".AspNetCore.OpenIdConnect.Nonce.", "nonce_cookie"),
        ("ConsentResponse.", "consent_response"),
    ];

    // Claims that hold times, shown relative to the clock's start.
    private static readonly HashSet<string> TimeClaims = new(StringComparer.Ordinal) { "auth_time", "iat", "exp", "nbf" };

    private static readonly HtmlParser HtmlParser = new();

    private readonly TestClock _clock;
    private readonly SnapshotScrubber _scrubber;

    internal SnapshotScrubber Scrubber => _scrubber;
    private readonly BrowserJournal _journal;

    internal TestBrowser(HttpMessageHandler handler, TestClock clock, SnapshotScrubber scrubber)
        : this(new BrowserJournal(), handler, clock, scrubber)
    {
    }

    private TestBrowser(BrowserJournal journal, HttpMessageHandler handler, TestClock clock, SnapshotScrubber scrubber)
        : base(new JournalHandler(journal, new CookieJarHandler(new CookieJar(clock), clock, handler)), disposeHandler: true)
    {
        _journal = journal;
        _journal.Browser = this;
        _clock = clock;
        _scrubber = scrubber;
    }

    /// <summary>
    /// Opens <paramref name="url" /> and follows redirects and automatically submitted forms (form_post responses) until
    /// a page needs input or the flow ends, and returns that page.
    /// </summary>
    public Task<Page> OpenAsync(Uri url, Ct cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        return NavigateAsync(HttpMethod.Get, url, form: null, initiator: null, cancellationToken);
    }

    /// <summary>Submits a page's form and follows on from there, like <see cref="OpenAsync" />.</summary>
    internal Task<Page> SubmitAsync(Page page, HtmlForm form, IReadOnlyDictionary<string, string> fields, Ct cancellationToken)
    {
        var action = string.IsNullOrEmpty(form.Action) ? page.Url : new Uri(page.Url, form.Action);
        return form.Method == "POST"
            ? NavigateAsync(HttpMethod.Post, action, fields, page.Url, cancellationToken)
            : NavigateAsync(HttpMethod.Get, new Uri(QueryHelpers.AddQueryString(action.GetLeftPart(UriPartial.Path), fields!)), null, page.Url, cancellationToken);
    }

    /// <summary>The requests this browser sent since the current scenario run started, as they appear in a snapshot.</summary>
    internal IReadOnlyList<BrowserStep> Journal => _journal.Steps;

    internal void ClearJournal() => _journal.Clear();

    /// <summary>
    /// Drives an authorization request by hand (design doc section 09): opens <paramref name="authorizeUrl" />, signs in
    /// as <paramref name="user" /> if a login page appears, and checks that the flow landed at the redirect URI
    /// <paramref name="authorizeUrl" /> asked for, with a code. Used when a scenario redeems the authorization code
    /// itself at the token endpoint, instead of letting a client app's OpenID Connect handler redeem it: that redirect
    /// URI should be one nothing maps (for example <see cref="IWebApp.ManualCallbackUrl" />), so the flow stops there
    /// with the code still visible instead of being redeemed automatically.
    /// </summary>
    public async Task<AuthorizationResponse> AuthorizeAsync(Uri authorizeUrl, TestUser user, Ct cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizeUrl);
        ArgumentNullException.ThrowIfNull(user);

        var expectedRedirectUri = new Uri(QueryHelpers.ParseQuery(authorizeUrl.Query)["redirect_uri"].ToString());

        var page = await OpenAsync(authorizeUrl, cancellationToken);
        if (page is LoginPage login)
        {
            page = await login.SignInAsAsync(user, cancellationToken);
        }

        if (!SameEndpoint(page.Url, expectedRedirectUri))
        {
            throw new PageAssertionException(
                $"Expected a redirect to {Describe(expectedRedirectUri)} with a code, got {page.GetType().Name} at {{{page.Url.Host}}}.");
        }

        var query = QueryHelpers.ParseQuery(page.Url.Query).ToDictionary(parameter => parameter.Key, parameter => parameter.Value.ToString());
        if (query.TryGetValue("error", out var error))
        {
            var description = query.TryGetValue("error_description", out var errorDescription) ? $": {errorDescription}" : "";
            throw new PageAssertionException($"Expected a redirect to {Describe(expectedRedirectUri)} with a code, got error '{error}'{description}.");
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            throw new PageAssertionException($"Expected a redirect to {Describe(expectedRedirectUri)} with a code, got none.");
        }

        return new AuthorizationResponse(code);
    }

    // Scheme, host, port and path must all match: the harness's host names are already readable (for example "web"),
    // so no scrubbing is needed here, the same as Page.ShouldBe's assertion messages.
    private static bool SameEndpoint(Uri actual, Uri expected) =>
        string.Equals(actual.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(actual.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
        && actual.Port == expected.Port
        && string.Equals(actual.AbsolutePath, expected.AbsolutePath, StringComparison.OrdinalIgnoreCase);

    private static string Describe(Uri url) => $"{{{url.Host}}}{url.AbsolutePath}";

    private async Task<Page> NavigateAsync(
        HttpMethod method,
        Uri url,
        IReadOnlyDictionary<string, string>? form,
        Uri? initiator,
        Ct cancellationToken)
    {
        for (var hop = 0; hop < MaxHops; hop++)
        {
            using var request = new HttpRequestMessage(method, url);
            if (form is not null)
            {
                request.Content = new FormUrlEncodedContent(form);
            }

            if (initiator is not null)
            {
                request.Options.Set(CookieJarHandler.Initiator, initiator);
            }

            using var response = await SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                if (response.StatusCode is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    throw new NotSupportedException(
                        $"{(int)response.StatusCode} {response.StatusCode} redirects (which must replay the original method and body) are not implemented in the outside-in harness yet.");
                }

                (method, url, form, initiator) = (HttpMethod.Get, new Uri(url, location), null, url);
                continue;
            }

            var page = RecognizePage(url, response, body);
            if (page is AutoSubmitPage autoSubmit)
            {
                // A form_post response: a real browser submits it with JavaScript, without user input.
                var action = string.IsNullOrEmpty(autoSubmit.Form.Action) ? url : new Uri(url, autoSubmit.Form.Action);
                (method, url, form, initiator) = (HttpMethod.Post, action, autoSubmit.Form.Inputs.ToDictionary(input => input.Name, input => input.Value), url);
                continue;
            }

            return page;
        }

        throw new InvalidOperationException($"The browser followed {MaxHops} redirects and form posts without reaching a page that needs input.");
    }

    private Page RecognizePage(Uri url, HttpResponseMessage response, string body)
    {
        var status = response.StatusCode;
        if (response.Headers.TryGetValues(PageHeader, out var pageHeader) && pageHeader.FirstOrDefault() == AppPage.Marker)
        {
            return new AppPage(this, url, status, DescribeClaims(body));
        }

        if (response.Content.Headers.ContentType?.MediaType != "text/html")
        {
            return new UnknownPage(this, url, status);
        }

        using var document = HtmlParser.ParseDocument(body);
        var marker = document.QuerySelector("[data-page]")?.GetAttribute("data-page");
        var form = document.Forms.Length > 0 ? HtmlForm.From((IHtmlFormElement)document.Forms[0]) : null;

        return marker switch
        {
            LoginPage.Marker when form is not null => new LoginPage(this, url, status, form),
            AppPage.Marker => new AppPage(this, url, status, claims: null),
            null when form is not null && form.Method == "POST" && form.Inputs.All(input => input.Type == "hidden") =>
                new AutoSubmitPage(this, url, status, form),
            _ => new UnknownPage(this, url, status),
        };
    }

    private Dictionary<string, object>? DescribeClaims(string json)
    {
        if (json.Length == 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(
            claim => claim.Name,
            claim => claim.Value.ValueKind == JsonValueKind.Array
                ? claim.Value.EnumerateArray().Select(value => DescribeClaim(claim.Name, value.ToString())).ToList()
                : (object)DescribeClaim(claim.Name, claim.Value.ToString()));
    }

    private string DescribeClaim(string type, string value) =>
        TimeClaims.Contains(type) && long.TryParse(value, CultureInfo.InvariantCulture, out var seconds)
            ? _clock.Describe(DateTimeOffset.FromUnixTimeSeconds(seconds))
            : Register(type, value);

    private BrowserStep DescribeStep(HttpMethod method, Uri url, IReadOnlyDictionary<string, string>? form, HttpResponseMessage response)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var headers)
            ? headers.Select(header => SetCookie.Parse(header, url, _clock.TimeProvider.GetUtcNow()))
                .OfType<SetCookie>()
                .Select(cookie => DescribeCookie(cookie, url))
                .ToList()
            : null;

        return new BrowserStep(
            $"{method} {url.GetLeftPart(UriPartial.Path)}",
            DescribeQuery(url.Query, url),
            form?.ToDictionary(field => field.Key, field => DescribeQueryValue(field.Key, field.Value, url)),
            $"{(int)response.StatusCode} {response.ReasonPhrase}",
            setCookies);
    }

    private Dictionary<string, object>? DescribeQuery(string query, Uri requestUrl)
    {
        var parameters = QueryHelpers.ParseQuery(query);
        return parameters.Count == 0
            ? null
            : parameters.ToDictionary(parameter => parameter.Key, parameter => DescribeQueryValue(parameter.Key, parameter.Value.ToString(), requestUrl));
    }

    // A parameter holding a URL with its own query (such as a login page's ReturnUrl) is split into URL and query too. A
    // relative URL is shown absolute, so a space's path prefix is part of its placeholder, such as {sut}.
    private object DescribeQueryValue(string name, string value, Uri requestUrl)
    {
        var queryStart = value.IndexOf('?', StringComparison.Ordinal);
        if (queryStart > 0 && (value.StartsWith('/') || value.StartsWith("https://", StringComparison.Ordinal)))
        {
            var url = new Uri(requestUrl, value[..queryStart]).ToString();
            return new UrlSnapshot(url, DescribeQuery(value[queryStart..], requestUrl));
        }

        // A JWT (an id_token in a form_post body, an id_token_hint on a logout URL, …) is decoded into its header and
        // claims instead of kept opaque (design doc section 10, D25/D26).
        return JwtFormatter.TryDescribe(value, _scrubber, _clock, out var decoded) ? decoded : Register(name, value);
    }

    private string DescribeCookie(SetCookie cookie, Uri requestUrl)
    {
        foreach (var (prefix, fieldName) in GeneratedCookieNames)
        {
            if (cookie.Name.StartsWith(prefix, StringComparison.Ordinal))
            {
                _scrubber.RegisterProtocolValue(fieldName, cookie.Name[prefix.Length..]);
            }
        }

        // The scope is shown as the absolute URL the cookie applies to, so a space's path prefix is part of {sut}.
        var attributes = new List<string> { new Uri(requestUrl, cookie.Path).ToString().TrimEnd('/') };
        if (cookie.Secure)
        {
            attributes.Add("secure");
        }

        if (cookie.HttpOnly)
        {
            attributes.Add("httponly");
        }

        if (cookie.SameSite is not null)
        {
            attributes.Add($"samesite={cookie.SameSite}");
        }

        if (cookie.Expires is { } expires)
        {
            attributes.Add(expires <= _clock.TimeProvider.GetUtcNow() ? "deleted" : $"expires={_clock.Describe(expires)}");
        }

        return $"{cookie.Name} ({string.Join(", ", attributes)})";
    }

    private string Register(string fieldName, string value)
    {
        if (ProtocolFields.Contains(fieldName))
        {
            _scrubber.RegisterProtocolValue(fieldName, value);
        }

        return value;
    }

    /// <summary>The browser's journal: every request it sent, described as it appears in a snapshot.</summary>
    private sealed class BrowserJournal
    {
        private readonly Lock _lock = new();
        private readonly List<BrowserStep> _steps = [];

        public TestBrowser? Browser { get; set; }

        public IReadOnlyList<BrowserStep> Steps
        {
            get
            {
                lock (_lock)
                {
                    return [.. _steps];
                }
            }
        }

        public void Add(BrowserStep step)
        {
            lock (_lock)
            {
                _steps.Add(step);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _steps.Clear();
            }
        }
    }

    /// <summary>Records every request the browser sends, whichever method sent it.</summary>
    private sealed class JournalHandler(BrowserJournal journal, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Ct cancellationToken)
        {
            IReadOnlyDictionary<string, string>? form = null;
            if (request.Content is FormUrlEncodedContent content)
            {
                form = QueryHelpers.ParseQuery(await content.ReadAsStringAsync(cancellationToken))
                    .ToDictionary(field => field.Key, field => field.Value.ToString());
            }

            var response = await base.SendAsync(request, cancellationToken);
            journal.Add(journal.Browser!.DescribeStep(request.Method, request.RequestUri!, form, response));
            return response;
        }
    }
}

/// <summary>One request in a browser's journal, as it appears in a snapshot.</summary>
/// <param name="Request">The method and URL without its query, for example <c>GET {sut}/connect/authorize</c>.</param>
/// <param name="Query">The query parameters, if any.</param>
/// <param name="Form">The form fields the browser posted, if any. URLs with a query are split like <paramref name="Query" />.</param>
/// <param name="Status">The response status.</param>
/// <param name="SetCookies">The cookies the response set or deleted: name and attributes, never the value.</param>
public sealed record BrowserStep(
    string Request,
    IReadOnlyDictionary<string, object>? Query,
    IReadOnlyDictionary<string, object>? Form,
    string Status,
    IReadOnlyList<string>? SetCookies);

/// <summary>A URL inside a query parameter, split into URL and query.</summary>
public sealed record UrlSnapshot(string Url, IReadOnlyDictionary<string, object>? Query);

/// <summary>
/// The authorization code <see cref="TestBrowser.AuthorizeAsync" /> captured at the redirect URI, instead of a client
/// app's OpenID Connect handler redeeming it.
/// </summary>
public sealed record AuthorizationResponse(string Code);
