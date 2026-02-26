// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Result of initiating an IdP-initiated SAML SSO flow.
/// </summary>
public class IdpInitiatedResult
{
    /// <summary>
    /// Gets whether the initiation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the URL to redirect to (login or callback endpoint).
    /// Only set when Success is true.
    /// </summary>
    public Uri? RedirectUrl { get; init; }

    /// <summary>
    /// Gets the validation error message.
    /// Only set when Success is false.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result with a redirect URL.
    /// </summary>
    public static IdpInitiatedResult Succeed(Uri redirectUrl) =>
        new() { Success = true, RedirectUrl = redirectUrl };

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static IdpInitiatedResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
