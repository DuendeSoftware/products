// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityModel;
using Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DuendeTestUserStore = Duende.IdentityServer.Test.TestUserStore;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// The purpose-built login pages a test adds to an IdentityServer (design doc section 07, D9). Each does one job.
/// IdentityServer's <c>UserInteraction.LoginUrl</c> points at <see cref="LoginPath" />.
/// </summary>
internal static class LoginPages
{
    public const string LoginPath = "/account/login";
    public const string ExternalCallbackPath = "/account/external-callback";

    private const string ReturnUrlItem = "returnUrl";
    private const string SchemeItem = "scheme";

    /// <summary>A username and password form, checked against IdentityServer's test users (D20).</summary>
    public static void MapPasswordLogin(WebApplication app)
    {
        app.MapGet(LoginPath, (string? returnUrl) => LoginForm(returnUrl, error: null));
        app.MapPost(LoginPath, async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();

            var users = context.RequestServices.GetRequiredService<DuendeTestUserStore>();
            if (!users.ValidateCredentials(username, password))
            {
                return LoginForm(returnUrl, "Invalid username or password.");
            }

            var user = users.FindByUsername(username);
            await context.SignInAsync(new IdentityServerUser(user.SubjectId)
            {
                DisplayName = user.Username,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
                AuthenticationMethods = { OidcConstants.AuthenticationMethods.Password },
            });
            return RedirectToReturnUrl(context, returnUrl);
        });
    }

    /// <summary>
    /// A username and password form (same markup and fields as <see cref="MapPasswordLogin" />, D9), checked against
    /// User Management instead of IdentityServer's test users (D20): the form posts the same <c>username</c> and
    /// <c>password</c> fields, so every permutation produces the same snapshot.
    /// </summary>
    public static void MapUserManagementPasswordLogin(WebApplication app)
    {
        app.MapGet(LoginPath, (string? returnUrl) => LoginForm(returnUrl, error: null));
        app.MapPost(LoginPath, async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();

            if (!NonValidatedPassword.TryCreate(password, out var suppliedPassword))
            {
                return LoginForm(returnUrl, "Invalid username or password.");
            }

            var authenticator = context.RequestServices.GetRequiredService<IPasswordAuthenticator>();
            var address = UserManagementProvisioning.LoginAddressFor(username);
            var result = await authenticator.TryAuthenticateAsync(OidcStandardAttributes.Email.Code, address.Value, suppliedPassword, context.RequestAborted);
            if (result is not PasswordAuthenticationResult.Success success)
            {
                return LoginForm(returnUrl, "Invalid username or password.");
            }

            await context.SignInAsync(new IdentityServerUser(success.UserSubjectId.Value)
            {
                DisplayName = username,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
                AuthenticationMethods = { OidcConstants.AuthenticationMethods.Password },
            });
            return RedirectToReturnUrl(context, returnUrl);
        });
    }

    /// <summary>Sends the user to the one configured external provider, and signs them in when they come back.</summary>
    public static void MapExternalLogin(WebApplication app)
    {
        app.MapGet(LoginPath, async (HttpContext context, string? returnUrl) =>
        {
            var providers = context.RequestServices.GetRequiredService<IIdentityProviderStore>();
            var schemes = (await providers.GetAllSchemeNamesAsync(context.RequestAborted)).Where(provider => provider.Enabled).ToList();
            if (schemes.Count != 1)
            {
                return Results.Problem($"The external login page needs exactly one enabled identity provider, but found {schemes.Count}.");
            }

            var properties = new AuthenticationProperties { RedirectUri = context.Request.PathBase + ExternalCallbackPath };
            properties.Items[ReturnUrlItem] = returnUrl ?? "/";
            properties.Items[SchemeItem] = schemes[0].Scheme;
            return Results.Challenge(properties, [schemes[0].Scheme]);
        });

        app.MapGet(ExternalCallbackPath, async (HttpContext context) =>
        {
            var result = await context.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            if (!result.Succeeded
                || result.Principal?.FindFirst(JwtClaimTypes.Subject)?.Value is not { } externalSubject
                || result.Properties?.Items.TryGetValue(SchemeItem, out var provider) != true
                || provider is null)
            {
                return Results.BadRequest("External authentication failed.");
            }

            var name = result.Principal.FindFirst(JwtClaimTypes.Name)?.Value ?? externalSubject;
            var space = context.RequestServices.GetService<ISpaceContextAccessor>()?.GetSpaceIdOrDefault();
            var user = context.RequestServices.GetRequiredService<DuendeTestUserStore>().FindByExternalProvider(provider, externalSubject)
                ?? context.RequestServices.GetRequiredService<InMemoryTopologyStores>().ProvisionExternalUser(provider, externalSubject, name, space);

            await context.SignInAsync(new IdentityServerUser(user.SubjectId) { DisplayName = user.Username, IdentityProvider = provider });
            await context.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            result.Properties.Items.TryGetValue(ReturnUrlItem, out var returnUrl);
            return RedirectToReturnUrl(context, returnUrl);
        });
    }

    /// <summary>Signs <paramref name="user" /> in straight away and returns to the authorize flow.</summary>
    public static void MapAutoSignIn(WebApplication app, TestUser user) =>
        app.MapGet(LoginPath, async (HttpContext context, string? returnUrl) =>
        {
            await context.SignInAsync(new IdentityServerUser(user.SubjectId)
            {
                DisplayName = user.Username,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
                AuthenticationMethods = { OidcConstants.AuthenticationMethods.Password },
            });
            return RedirectToReturnUrl(context, returnUrl);
        });

    private static IResult RedirectToReturnUrl(HttpContext context, string? returnUrl) =>
        context.RequestServices.GetRequiredService<IIdentityServerInteractionService>().IsValidReturnUrl(returnUrl)
            ? Results.Redirect(returnUrl!)
            : Results.BadRequest("Invalid return URL.");

    private static IResult LoginForm(string? returnUrl, string? error) =>
        Results.Content(
            $"""
            <!DOCTYPE html>
            <html>
            <body data-page="login">
              {(error is null ? "" : $"<p class=\"error\">{WebUtility.HtmlEncode(error)}</p>")}
              <form method="post">
                <input type="hidden" name="returnUrl" value="{WebUtility.HtmlEncode(returnUrl)}" />
                <input type="text" name="username" />
                <input type="password" name="password" />
                <button type="submit">Sign in</button>
              </form>
            </body>
            </html>
            """,
            "text/html");
}
