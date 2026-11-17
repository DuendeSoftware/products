// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Totp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// HTTP endpoint handlers for authentication operations
/// </summary>
public static class AuthenticationEndpointHandlers
{
    /// <summary>
    /// Gets the authentication configuration for the web component
    /// </summary>
    public static IResult GetAuthConfig(
        [FromServices] WebComponentOptions options)
    {
        var config = WebComponentConfigurationBuilder.Build(options);
        return Results.Json(config);
    }

    /// <summary>
    /// Sends an OTP to the specified email address
    /// </summary>
    public static async Task<IResult> SendOtpAsync(
        HttpContext context,
        [FromBody] SendOtpRequest request,
        [FromServices] IOtpSender otpSender,
        [FromServices] CookieOtpStateService cookieOtpStateService)
    {
        try
        {
            var emailAddress = EmailAddress.Create(request.Email);

            switch (await otpSender.TrySendOtpAsync(new OtpAddress(OtpChannel.Email, emailAddress), context.RequestAborted))
            {
                case SendOtpResult.Sent sent:
                    var otpData = new OtpData(request.Email, sent.Token.ToString(), ReturnUrl: null);
                    cookieOtpStateService.StoreOtpData(otpData);
                    break;
                case SendOtpResult.Blocked blocked:
                    // TODO: blocked.SendingBlockedFor.TotalSeconds should really be written into a Retry-After header
                    return Results.Json(AuthenticationComponentResponse.ErrorResult($"Failed to send OTP. Try again in {Math.Ceiling(blocked.SendingBlockedFor.TotalSeconds)} second(s)."), statusCode: 429);
                default:
                    return Results.Json(AuthenticationComponentResponse.ErrorResult("Failed to send OTP"), statusCode: 500);
            }

            return Results.Json(AuthenticationComponentResponse.RequiresOtpVerification(data: new { email = request.Email }));
        }
        catch (Exception ex)
        {
            return Results.Json(AuthenticationComponentResponse.ErrorResult(ex.Message), statusCode: 500);
        }
    }

    /// <summary>
    /// Verifies an OTP code
    /// </summary>
    public static async Task<IResult> VerifyOtpAsync(
        HttpContext context,
        [FromBody] VerifyOtpRequest request,
        [FromServices] IOtpAuthenticator otpAuthenticator,
        [FromServices] IUserAuthenticatorsSelfService selfService,
        [FromServices] CookieOtpStateService cookieOtpStateService,
        [FromServices] CookieAuthenticationStateService cookieAuthenticationStateService,
        [FromServices] WebComponentOptions options)
    {
        try
        {
            if (!cookieOtpStateService.TryRetrieveOtpData(out var otpData))
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("Invalid or expired session"), statusCode: 400);
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("Verification code is required"), statusCode: 400);
            }

            if (await otpAuthenticator.TryAuthenticateAsync(
                PlainTextOtp.Create(request.Code),
                OtpToken.Create(otpData.Token),
                context.RequestAborted) is not OtpAuthenticationResult.Success result)
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("Invalid or expired code"), statusCode: 401);
            }

            cookieOtpStateService.ClearOtpData();

            if (await selfService.TryGetAsync(result.UserSubjectId, context.RequestAborted) is not { } authenticators)
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("Failed to retrieve user details"), statusCode: 500);
            }

            if (authenticators.TotpDeviceNames.Count > 0)
            {
                // Store authentication state for MFA challenge
                var authState = new AuthenticationStateData(
                    UserId: authenticators.SubjectId.ToString(),
                    Email: result.Address.SubjectId.ToString(),
                    RememberMe: false,
                    ReturnUrl: otpData.ReturnUrl
                );

                cookieAuthenticationStateService.StoreAuthenticationState(authState);
                return Results.Json(AuthenticationComponentResponse.RequiresMfa(data: new { email = result.Address.SubjectId.ToString() }), statusCode: 200);
            }

            await CreateAuthenticationSessionAsync(context, result.UserSubjectId.ToString(), result.Address.SubjectId.ToString(), rememberMe: false,
                authenticationMethod: "otp");

            return Results.Json(AuthenticationComponentResponse.SuccessResult());
        }
        catch (Exception ex)
        {
            return Results.Json(AuthenticationComponentResponse.ErrorResult(ex.Message), statusCode: 500);
        }
    }

    /// <summary>
    /// Initiates an external authentication challenge
    /// </summary>
    public static IResult ExternalChallengeAsync(
        HttpContext context,
        string provider,
        [FromServices] WebComponentOptions options)
    {
        if (options.Authentication.ExternalProviders
                .FirstOrDefault(p => p.Id.Equals(provider, StringComparison.OrdinalIgnoreCase))
            is not { } externalProvider)
        {
            return Results.BadRequest(new { error = "Unsupported provider" });
        }

        var schemeName = externalProvider.AuthenticationScheme;

        var authProperties = new AuthenticationProperties { RedirectUri = "/" };
        return Results.Challenge(authProperties, new[] { schemeName }!);
    }

    /// <summary>
    /// Verifies an MFA (TOTP) code
    /// </summary>
    public static async Task<IResult> VerifyMfaAsync(
        HttpContext context,
        [FromBody] VerifyMfaRequest request,
        [FromServices] ITotpAuthenticator totpAuthenticator,
        [FromServices] IUserAuthenticatorsSelfService selfService,
        [FromServices] CookieAuthenticationStateService cookieAuthenticationStateService)
    {
        try
        {
            if (!cookieAuthenticationStateService.TryRetrieveAuthenticationState(out var authState))
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("Invalid or expired session"), statusCode: 400);
            }

            if (await selfService.TryGetAsync(UserSubjectId.Create(authState.UserId), context.RequestAborted) is not { } user)
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("User not found"), statusCode: 404);
            }

            var cleanCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            if (!PlainTextTotp.TryCreate(cleanCode, out var totp))
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("Invalid code format"), statusCode: 400);
            }

            var isValid = await totpAuthenticator.TryAuthenticateAsync(
                UserSubjectId.Create(authState.UserId),
                user.TotpDeviceNames.First(),
                totp.Value,
                context.RequestAborted);

            if (!isValid)
            {
                return Results.Json(AuthenticationComponentResponse.ErrorResult("Invalid code"), statusCode: 401);
            }

            await CreateAuthenticationSessionAsync(context, authState.UserId, authState.Email, authState.RememberMe, "mfa");
            cookieAuthenticationStateService.ClearAuthenticationState();

            return Results.Json(AuthenticationComponentResponse.SuccessResult());
        }
        catch (Exception ex)
        {
            return Results.Json(AuthenticationComponentResponse.ErrorResult(ex.Message), statusCode: 500);
        }
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    public static async Task<IResult> LogoutAsync(
        HttpContext context,
        [FromServices] CookieAuthenticationStateService cookieAuthenticationStateService,
        [FromServices] CookieOtpStateService cookieOtpStateService)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Clear any temporary authentication state cookies
        cookieAuthenticationStateService.ClearAuthenticationState();
        cookieOtpStateService.ClearOtpData();

        return Results.Redirect("/");
    }

    private static async Task CreateAuthenticationSessionAsync(
        HttpContext context,
        string userId,
        string email,
        bool rememberMe,
        string authenticationMethod)
    {
        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, userId),
            new(JwtClaimTypes.Name, email),
            new(JwtClaimTypes.Email, email),
            new(JwtClaimTypes.AuthenticationMethod, authenticationMethod)
        };

        var claimsIdentity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: JwtClaimTypes.Name,
            roleType: JwtClaimTypes.Role);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity), authProperties);
    }

    /// <summary>
    /// Cancels the current authentication flow and clears any temporary state
    /// Used when user clicks "back" or wants to restart the flow
    /// </summary>
    public static IResult CancelAuthenticationFlow(
        HttpContext context,
        [FromServices] CookieAuthenticationStateService cookieAuthenticationStateService,
        [FromServices] CookieOtpStateService cookieOtpStateService)
    {
        // Clear any temporary authentication state cookies
        cookieAuthenticationStateService.ClearAuthenticationState();
        cookieOtpStateService.ClearOtpData();

        return Results.Json(AuthenticationComponentResponse.SuccessResult(
            nextAction: WebComponentActions.UnifiedForm
        ));
    }

    public record SendOtpRequest(string Email);

    public record VerifyOtpRequest(string Code);

    public record VerifyMfaRequest(string Code);
}
