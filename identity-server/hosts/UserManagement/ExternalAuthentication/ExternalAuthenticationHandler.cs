// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authentication;

namespace ExternalAuthentication;

/// <summary>
/// Handles external authentication ticket processing, including user registration and authenticator management.
/// </summary>
public class ExternalAuthenticationHandler
{
    /// <summary>
    /// The state parameter
    /// </summary>
    public const string UserIdStateParameter = "UserId";
    public const string SchemeNameParameter = "Scheme";

    public static async Task OnTicketReceivedAsync(TicketReceivedContext context)
    {
        var ct = context.HttpContext.RequestAborted;
        var principal = context.Principal ?? throw new InvalidOperationException("No principal");
        var selfService = context.HttpContext.RequestServices.GetRequiredService<IUserSelfService>();
        var authenticator = context.HttpContext.RequestServices.GetRequiredService<IExternalAuthenticator>();

        var externalAuthenticatorName = ExternalAuthenticatorName.Create(context.Scheme.Name);
        var sub = principal.FindFirst(JwtClaimTypes.Subject) ?? throw new InvalidOperationException("No subject");
        var address = new ExternalAuthenticatorAddress(externalAuthenticatorName, OpaqueSubjectId.Create(sub.Value));
        UserProfile? profile;

        // Check if this is an "add authenticator" flow by looking for the userId in the state
        if (context.Properties is not null &&
            context.Properties.Items.TryGetValue(UserIdStateParameter, out var userIdString))
        {
            if (userIdString is null ||
                !context.Properties.Items.TryGetValue(SchemeNameParameter, out var schemeParameter) ||
                schemeParameter != context.Scheme.Name)
            {
                RedirectToSignInError(context, SignInError.CouldNotAddAuthenticator);
                return;
            }

            if ((profile = await selfService.Profiles.TryGetAsync(UserSubjectId.Create(userIdString), ct)) is null)
            {
                RedirectToSignInError(context, SignInError.UserNotFoundForAddAuthenticator);
                return;
            }

            if (!await selfService.Authenticators.TryAddExternalAuthenticatorAddressAsync(profile.SubjectId, address, ct))
            {
                RedirectToSignInError(context, SignInError.CouldNotAddAuthenticator);
                return;
            }
        }
        else
        {
            if (await authenticator.TryAuthenticateAsync(address, ct) is not
                ExternalAuthenticationResult.Success success)
            {
                RedirectToSignInError(context, SignInError.CouldNotCreateUser);
                return;
            }

            if ((profile = await selfService.Profiles.TryGetAsync(success.UserSubjectId, ct)) is null)
            {
                var nameClaim = principal.FindFirstValue(JwtClaimTypes.Name) ?? string.Empty;
                var schema = await selfService.Profiles.GetSchemaAsync(ct);
                var attributes = new AttributeValueCollection(schema);
                attributes.Set(UserAttributes.Name, nameClaim);

                profile = await selfService.Profiles.TryCreateAsync(success.UserSubjectId, attributes.Validate(), ct);
                if (profile is null)
                {
                    RedirectToSignInError(context, SignInError.CouldNotCreateUser);
                    return;
                }
            }
        }

        var name = (string?)profile.Attributes.GetValueOrDefault(UserAttributes.Name)?.UntypedValue;

        Claim[] claims =
        [
            new (JwtClaimTypes.Subject, profile.SubjectId.ToString()),
            new (JwtClaimTypes.Name, name ?? "")
        ];

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "remote"));
    }

    private static void RedirectToSignInError(TicketReceivedContext context, SignInError error)
    {
        context.Response.Redirect($"/SignInError?error={error}");
        context.HandleResponse();
    }
}
