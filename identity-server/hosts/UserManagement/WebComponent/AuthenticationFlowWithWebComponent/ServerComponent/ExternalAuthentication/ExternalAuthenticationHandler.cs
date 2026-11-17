// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Microsoft.AspNetCore.Authentication;

namespace AuthenticationFlowWithWebComponent.ServerComponent.ExternalAuthentication;

public class ExternalAuthenticationHandler
{
    public static async Task OnTicketReceivedAsync(TicketReceivedContext context)
    {
        var ct = context.HttpContext.RequestAborted;
        var principal = context.Principal ?? throw new InvalidOperationException("No principal");
        var userSelfService =
            context.HttpContext.RequestServices.GetRequiredService<IUserSelfService>();
        var authenticator =
            context.HttpContext.RequestServices.GetRequiredService<IExternalAuthenticator>();

        var externalAuthenticatorName = ExternalAuthenticatorName.Create(context.Scheme.Name);
        var sub = principal.FindFirst(JwtClaimTypes.Subject) ?? throw new InvalidOperationException("No subject");
        var address = new ExternalAuthenticatorAddress(externalAuthenticatorName, OpaqueSubjectId.Create(sub.Value));

        if (await authenticator.TryAuthenticateAsync(address, ct) is not
                ExternalAuthenticationResult.Success success ||
            await userSelfService.Authenticators.TryGetAsync(success.UserSubjectId, ct) is not { } authenticators)
        {
            RedirectToSignInError(context, SignInError.CouldNotRegisterUser);
            return;
        }

        if (await userSelfService.Profiles.TryGetAsync(authenticators.SubjectId, ct) is not { } profile)
        {
            var schema = await userSelfService.Profiles.GetSchemaAsync(ct);
            var nameClaim = principal.FindFirstValue(JwtClaimTypes.Name) ?? string.Empty;
            var attributes = new AttributeValueCollection(schema);
            attributes.Set(UserAttributes.Name, nameClaim);

            profile = await userSelfService.Profiles.TryCreateAsync(authenticators.SubjectId, attributes.Validate(), ct: ct);

            if (profile is null)
            {
                RedirectToSignInError(context, SignInError.CouldNotRegisterUser);
                return;
            }
        }

        // Check if user has MFA configured
        var hasTotpConfigured = authenticators.TotpDeviceNames.Count > 0;

        if (hasTotpConfigured)
        {
            // Store authentication state for MFA challenge and redirect to MFA page
            var cookieAuthenticationStateService =
                context.HttpContext.RequestServices.GetRequiredService<CookieAuthenticationStateService>();
            var email = principal.FindFirstValue(JwtClaimTypes.Email) ?? authenticators.SubjectId.ToString();

            var authState = new AuthenticationStateData(
                UserId: authenticators.SubjectId.ToString(),
                Email: email,
                RememberMe: false,
                ReturnUrl: null
            );

            cookieAuthenticationStateService.StoreAuthenticationState(authState);

            // Redirect to home page - component will detect MFA requirement via /api/auth/state endpoint
            context.Response.Redirect("/");
            context.HandleResponse();
            return;
        }

        var name = (string?)profile.Attributes.GetValueOrDefault(UserAttributes.Name)?.UntypedValue;

        Claim[] claims =
        [
            new(JwtClaimTypes.Subject, profile.SubjectId.ToString()),
            new(JwtClaimTypes.Name, name ?? "")
        ];

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "remote"));
    }

    private static void RedirectToSignInError(TicketReceivedContext context, SignInError error)
    {
        context.Response.Redirect($"/SignInError?error={error}");
        context.HandleResponse();
    }
}
