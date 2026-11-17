// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// Handler for getting the current authentication state
/// </summary>
public static class AuthenticationStateHandler
{
    /// <summary>
    /// Gets the current authentication state for the user
    /// Checks if user is authenticated or has pending MFA/OTP verification
    /// </summary>
    public static async Task<IResult> GetAuthenticationStateAsync(
        HttpContext context,
        [FromServices] CookieAuthenticationStateService cookieAuthenticationStateService,
        [FromServices] CookieOtpStateService cookieOtpStateService)
    {
        // Check if user has a valid authentication cookie
        var authenticateResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (authenticateResult is { Succeeded: true, Principal.Identity.IsAuthenticated: true })
        {
            var email = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email) ??
                       authenticateResult.Principal.FindFirstValue("email") ?? "";

            return Results.Json(new AuthenticationStateResponse
            {
                Authenticated = true,
                NextState = "authenticated",
                Email = email
            });
        }

        // Check if user has pending MFA challenge
        if (cookieAuthenticationStateService.TryRetrieveAuthenticationState(out var authState))
        {
            return Results.Json(new AuthenticationStateResponse
            {
                Authenticated = false,
                NextState = "mfa-required",
                SessionId = "cookie",
                Email = authState.Email
            });
        }

        // Check if user has pending OTP verification
        if (cookieOtpStateService.TryRetrieveOtpData(out var otpData))
        {
            return Results.Json(new AuthenticationStateResponse
            {
                Authenticated = false,
                NextState = "otp-verify",
                SessionId = "cookie",
                Email = otpData.Email
            });
        }

        // No authentication state - user needs to start authentication
        return Results.Json(new AuthenticationStateResponse
        {
            Authenticated = false,
            NextState = "unified-form"
        });
    }
}

/// <summary>
/// Response model for authentication state endpoint
/// </summary>
public record AuthenticationStateResponse
{
    public required bool Authenticated { get; init; }
    public required string NextState { get; init; }
    public string? SessionId { get; init; }
    public string? Email { get; init; }
}
