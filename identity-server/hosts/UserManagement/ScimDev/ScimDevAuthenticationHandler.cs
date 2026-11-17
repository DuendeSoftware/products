// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ScimDev;

internal static class ScimDevAuthenticationDefaults
{
    public const string Policy = "ScimDev";
    public const string Scheme = "ScimDevBearer";
}

public sealed class ScimDevCredential(string token)
{
    private readonly byte[] _tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public bool Matches(string token)
    {
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(_tokenHash, tokenHash);
    }
}

/// <summary>
/// Adapts SCIM Verify's opaque bearer token to ASP.NET Core authentication.
/// </summary>
public sealed class ScimDevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ScimDevCredential credential)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorizationHeaders = Request.Headers.Authorization;
        if (authorizationHeaders.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (authorizationHeaders.Count != 1 ||
            !AuthenticationHeaderValue.TryParse(authorizationHeaders[0], out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer token."));
        }

        if (!credential.Matches(authorization.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer token."));
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "scim.dev") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
