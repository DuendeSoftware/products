// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication;
using SamplesShared;

namespace PasskeyAuthentication.Sdk;

internal static class WebApplicationExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static void MapPasskeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/passkeys");

        // POST /passkeys/register/begin
        _ = group.MapPost("/register/begin", async (IPasskeyCeremonies ceremonies, HttpContext context, Ct ct) =>
        {
            var email = context.User.GetEmailAddress().ToString();
            var session = await ceremonies.BeginRegistrationAsync(context.User.GetSubjectId(), email, email, ct);
            return Results.Json(new { challengeId = session.ChallengeId, options = session.Options }, JsonOptions);
        }).RequireAuthorization();

        // POST /passkeys/register/complete
        _ = group.MapPost("/register/complete", async (
                IPasskeyCeremonies ceremonies,
                IUserAuthenticatorsSelfService selfService,
                HttpContext context,
                PasskeyCompleteRegistrationRequest request,
                Ct ct) =>
            await ceremonies.CompleteRegistrationAsync(request, ct) switch
            {
                PasskeyRegistrationCompleteResult.Success success =>
                    await selfService.TryAddPasskeyAsync(context.User.GetSubjectId(), success.Credential, ct)
                        ? Results.Json(new { credentialId = success.Credential.CredentialId.ToString() })
                        : Results.StatusCode(500),
                PasskeyRegistrationCompleteResult.Failure =>
                    Results.Problem("Failed to complete passkey registration", statusCode: 400),
                _ => Results.StatusCode(500)
            }).RequireAuthorization();

        // DELETE /passkeys
        _ = group.MapDelete("/", async (
            IUserAuthenticatorsSelfService selfService, HttpContext context, string credentialId, Ct ct) =>
        {
            var id = PasskeyCredentialId.From(Convert.FromBase64String(credentialId));
            return await selfService.TryRemovePasskeyAsync(context.User.GetSubjectId(), id, ct)
                ? Results.Ok()
                : Results.Problem("Failed to remove passkey", statusCode: 400);
        }).RequireAuthorization();

        // POST /passkeys/authenticate/begin
        _ = group.MapPost("/authenticate/begin", async (IPasskeyCeremonies ceremonies, Ct ct) =>
            await ceremonies.BeginAuthenticationAsync(ct) switch
            {
                PasskeyAuthenticationBeginResult.Success success =>
                    Results.Json(
                        new { challengeId = success.Session.ChallengeId, options = success.Session.Options },
                        JsonOptions),
                PasskeyAuthenticationBeginResult.Failure =>
                    Results.Problem("Failed to authenticate with passkey", statusCode: 400),
                _ => Results.StatusCode(500)
            });

        // POST /passkeys/authenticate/complete
        _ = group.MapPost("/authenticate/complete", async (
            IPasskeyCeremonies ceremonies,
            IUserAuthenticatorsSelfService selfService,
            HttpContext context,
            PasskeyCompleteAuthenticationRequest request,
            Ct ct) =>
        {
            if (await ceremonies.CompleteAuthenticationAsync(request, ct) is not
                PasskeyAuthenticationCompleteResult.Success success)
            {
                return Results.Problem("Failed to authenticate with passkey", statusCode: 400);
            }

            if (await selfService.TryGetAsync(success.UserSubjectId, ct) is not { } user)
            {
                return Results.Problem("Failed to authenticate with passkey.", statusCode: 400);
            }

            await context.SignInAsync(ClaimsPrincipal.Create(
                user.SubjectId,
                (EmailAddress)user.OtpAddresses.Single(a => a.Channel == OtpChannel.Email).SubjectId,
                "passkey",
                null));

            return Results.Json(new { userVerified = success.UserVerified, backedUp = success.BackedUp });
        });
    }
}
