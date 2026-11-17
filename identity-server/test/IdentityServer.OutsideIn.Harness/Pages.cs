// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.OutsideIn.Harness.Internal;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// A page the <see cref="TestBrowser" /> stopped at because it needs input, or because the flow ended there (design doc
/// D39). The harness's own pages are recognized by a fixed marker (<c>data-page="login"</c>), so a test states which
/// page it expects with <see cref="ShouldBe{T}(IIssuer)" /> and acts on it.
/// </summary>
public abstract class Page(TestBrowser browser, Uri url, HttpStatusCode status)
{
    internal TestBrowser Browser { get; } = browser;

    /// <summary>The page's URL.</summary>
    public Uri Url { get; } = url;

    public HttpStatusCode Status { get; } = status;

    /// <summary>Checks this is a <typeparamref name="T" /> served by <paramref name="on" />, and returns it as one.</summary>
    public T ShouldBe<T>(IIssuer on) where T : Page => ShouldBe<T>(on.Url, on.Name);

    /// <summary>Checks this is a <typeparamref name="T" /> served by <paramref name="on" />, and returns it as one.</summary>
    public T ShouldBe<T>(IIdentityServer on) where T : Page => ShouldBe<T>(on.BaseUrl, on.Name);

    /// <summary>Checks this is a <typeparamref name="T" /> served by <paramref name="on" />, and returns it as one.</summary>
    public T ShouldBe<T>(IWebApp on) where T : Page => ShouldBe<T>(on.BaseUrl, on.Name);

    /// <summary>How the page appears in a snapshot.</summary>
    internal virtual object Describe() => new { Page = GetType().Name, Url = Url.GetLeftPart(UriPartial.Path) };

    private T ShouldBe<T>(Uri serverUrl, string serverName) where T : Page
    {
        if (this is T page && SameServer(Url, serverUrl))
        {
            return page;
        }

        throw new PageAssertionException(
            $"Expected {typeof(T).Name} at {{{serverName}}}, got {GetType().Name} at {{{Url.Host}}}: " +
            $"{(int)Status} {Status} for {Url.GetLeftPart(UriPartial.Path)}.");
    }

    // Host and port must match. For a path-based issuer (e.g. "/t/space-2"), the page's path must also start with
    // that path base at a segment boundary, so "/t/space-2/..." matches but "/t/space-20" or "/" doesn't.
    private static bool SameServer(Uri pageUrl, Uri serverUrl)
    {
        if (!string.Equals(pageUrl.Host, serverUrl.Host, StringComparison.OrdinalIgnoreCase) || pageUrl.Port != serverUrl.Port)
        {
            return false;
        }

        var pathBase = serverUrl.AbsolutePath.TrimEnd('/');
        if (pathBase.Length == 0)
        {
            return true;
        }

        return pageUrl.AbsolutePath.Equals(pathBase, StringComparison.OrdinalIgnoreCase)
            || pageUrl.AbsolutePath.StartsWith(pathBase + "/", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A login page served by the harness (<c>WithPasswordLogin()</c>). The page decides how; the test decides who.</summary>
public sealed class LoginPage : Page
{
    internal const string Marker = "login";

    internal LoginPage(TestBrowser browser, Uri url, HttpStatusCode status, HtmlForm form)
        : base(browser, url, status) => Form = form;

    internal HtmlForm Form { get; }

    /// <summary>Signs in as <paramref name="user" />, and follows on to the next page that needs input.</summary>
    public Task<Page> SignInAsAsync(TestUser user, Ct cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var fields = Form.Inputs.ToDictionary(input => input.Name, input => input.Value);
        fields["username"] = user.Username;
        fields["password"] = user.Password;
        return Browser.SubmitAsync(this, Form, fields, cancellationToken);
    }
}

/// <summary>The client app after the flow (design doc section 09). Its claims come from its user info endpoint.</summary>
public sealed class AppPage : Page
{
    internal const string Marker = "app";

    internal AppPage(TestBrowser browser, Uri url, HttpStatusCode status, IReadOnlyDictionary<string, object>? claims)
        : base(browser, url, status) => Claims = claims;

    /// <summary>The signed-in user's claims, if the page is the app's user info endpoint (<c>WithUserInfoEndpoint()</c>).</summary>
    public IReadOnlyDictionary<string, object>? Claims { get; }

    /// <summary>
    /// Checks the app shows <paramref name="user" /> as signed in: its <c>sub</c> and <c>name</c> claims are that user's
    /// subject ID and username, which are unique to the user's issuer. With Spaces on, this fails if a user from
    /// another space shows up. The message names what the app showed, for example <c>{user:alice@space-1}</c>.
    /// </summary>
    public AppPage ShouldBeSignedInAs(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (Claims is null)
        {
            throw new PageAssertionException(
                $"Expected the app to show {{user:{NameOf(user)}}} of {user.Issuer} as signed in, but {Url.GetLeftPart(UriPartial.Path)} shows no claims.");
        }

        var subject = Claim("sub");
        var name = Claim("name");
        if (subject != user.SubjectId || name != user.Username)
        {
            throw new PageAssertionException(
                $"Expected the app to show {{user:{NameOf(user)}}} of {user.Issuer} as signed in, " +
                $"got sub {Describe(subject, user)} and name {Describe(name, user)}.");
        }

        return this;
    }

    internal override object Describe() => new { Page = nameof(AppPage), Url = Url.GetLeftPart(UriPartial.Path), Claims };

    private string? Claim(string type) => Claims!.TryGetValue(type, out var value) ? value as string : null;

    // The name the user was created with: "alice" for "alice-sut-3f2a91c0".
    private static string NameOf(TestUser user) => user.Username[..user.Username.IndexOf('-', StringComparison.Ordinal)];

    private string Describe(string? value, TestUser user) =>
        value is null ? "(missing)" : Browser.Scrubber.Describe(value, user.Issuer);
}

/// <summary>A page the harness doesn't know: no marker it recognizes. Shows where the flow ended up.</summary>
public sealed class UnknownPage : Page
{
    internal UnknownPage(TestBrowser browser, Uri url, HttpStatusCode status)
        : base(browser, url, status)
    {
    }

    internal override object Describe() => new { Page = nameof(UnknownPage), Url = Url.GetLeftPart(UriPartial.Path), Status = $"{(int)Status} {Status}" };
}

/// <summary>A page that submits itself, such as a form_post response. The browser submits it and never returns it.</summary>
internal sealed class AutoSubmitPage : Page
{
    internal AutoSubmitPage(TestBrowser browser, Uri url, HttpStatusCode status, HtmlForm form)
        : base(browser, url, status) => Form = form;

    internal HtmlForm Form { get; }
}

/// <summary>Thrown when a page isn't the one a test expected.</summary>
public sealed class PageAssertionException(string message) : Exception(message);
