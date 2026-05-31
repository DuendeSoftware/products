// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// The default HTTP result returned after a successful passkey authentication sign-in.
/// Contains information about whether the user was verified and whether the credential is backed up.
/// </summary>
public sealed class PasskeyCompleteAuthenticationResult(bool userVerified, bool backedUp) : IResult
{
    /// <summary>
    /// Whether the user was verified during the passkey ceremony.
    /// </summary>
    public bool UserVerified => userVerified;

    /// <summary>
    /// Whether the passkey credential is backed up.
    /// </summary>
    public bool BackedUp => backedUp;

    /// <inheritdoc/>
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        httpContext.Response.Headers["Pragma"] = "no-cache";
        httpContext.Response.ContentType = "application/json";
        var dto = new { userVerified = UserVerified, backedUp = BackedUp };
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, dto, cancellationToken: httpContext.RequestAborted);
    }
}
