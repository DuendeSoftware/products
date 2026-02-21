// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer;

/// <summary>
/// Useful helpers for interacting with IdentityServer.
/// </summary>
public interface IIdentityServerTools
{
    /// <summary>
    /// Issues a JWT with a specific lifetime and set of claims.
    /// </summary>
    /// <param name="lifetime">The lifetime, in seconds, which will determine
    /// the exp claim of the token.</param>
    /// <param name="claims">A collection of additional claims to include in the
    /// token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A JWT that expires after the specified lifetime and contains
    /// the given claims.</returns>
    /// <remarks>Typical implementations depend on the <see cref="HttpContext"/>
    /// or <see cref="IdentityServerOptions.IssuerUri"/> to determine the issuer
    /// of the token. Ensure that calls to this method will only occur if there
    /// is an incoming HTTP request or with the option set.
    /// </remarks>
    Task<string> IssueJwtAsync(int lifetime, IEnumerable<Claim> claims, CT ct);

    /// <summary>
    /// Issues a JWT with a specific lifetime, issuer, and set of claims.
    /// </summary>
    /// <param name="lifetime">The lifetime, in seconds, which will determine
    /// the exp claim of the token.</param>
    /// <param name="issuer">The issuer of the token, set in the iss
    /// claim.</param>
    /// <param name="claims">A collection of additional claims to include in the
    /// token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A JWT with the specified lifetime, issuer and additional
    /// claims.</returns>
    Task<string> IssueJwtAsync(int lifetime, string issuer, IEnumerable<Claim> claims, CT ct);

    /// <summary>
    /// Issues a JWT with a specific lifetime, issuer, token type, and set of
    /// claims.
    /// </summary>
    /// <param name="lifetime">The lifetime, in seconds, which will determine
    /// the exp claim of the token.</param>
    /// <param name="issuer">The issuer of the token, set in the iss
    /// claim.</param>
    /// <param name="tokenType">The token's type, such as "access_token" or
    /// "id_token", set in the typ claim.</param>
    /// <param name="claims">A collection of additional claims to include in the
    /// token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A JWT with the specified lifetime, issuer, token type, and
    /// additional claims.</returns>
    Task<string> IssueJwtAsync(int lifetime, string issuer, string tokenType, IEnumerable<Claim> claims, CT ct);

    /// <summary>
    /// Issues a JWT access token for a particular client.
    /// </summary>
    /// <param name="clientId">The client identifier, set in the client_id
    /// claim.</param>
    /// <param name="lifetime">The lifetime, in seconds, which will determine
    /// the exp claim of the token.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="scopes">A collection of scopes, which will be added to the
    /// token as claims with the "scope" type.</param>
    /// <param name="audiences">A collection of audiences, which will be added
    /// to the token as claims with the "aud" type.</param>
    /// <param name="additionalClaims">A collection of additional claims to
    /// include in the token.</param>
    /// <returns>A JWT with the specified client, lifetime, scopes, audiences,
    /// and additional claims.</returns>
    /// <remarks>Typical implementations depend on the <see cref="HttpContext"/>
    /// or <see cref="IdentityServerOptions.IssuerUri"/> to determine the issuer
    /// of the token. Ensure that calls to this method will only occur if there
    /// is an incoming HTTP request or with the option set.
    /// </remarks>
    Task<string> IssueClientJwtAsync(
        string clientId,
        int lifetime,
        CT ct,
        IEnumerable<string>? scopes = null,
        IEnumerable<string>? audiences = null,
        IEnumerable<Claim>? additionalClaims = null);
}

/// <summary>
/// Class for useful helpers for interacting with IdentityServer
/// </summary>
public class IdentityServerTools : IIdentityServerTools
{
    private readonly IIssuerNameService _issuerNameService;
    private readonly ITokenCreationService _tokenCreation;
    private readonly TimeProvider _timeProvider;
    private readonly IdentityServerOptions _options;

    /// <inheritdoc/>
    public IdentityServerTools(IIssuerNameService issuerNameService, ITokenCreationService tokenCreation, TimeProvider timeProvider, IdentityServerOptions options)
    {
        _issuerNameService = issuerNameService;
        _tokenCreation = tokenCreation;
        _timeProvider = timeProvider;
        _options = options;
    }

    /// <inheritdoc/>
    public virtual async Task<string> IssueJwtAsync(int lifetime, IEnumerable<Claim> claims, CT ct)
    {
        var issuer = await _issuerNameService.GetCurrentAsync(ct);
        return await IssueJwtAsync(lifetime, issuer, claims, ct);
    }

    /// <inheritdoc/>
    public virtual Task<string> IssueJwtAsync(int lifetime, string issuer, IEnumerable<Claim> claims, CT ct)
    {
        var tokenType = OidcConstants.TokenTypes.AccessToken;
        return IssueJwtAsync(lifetime, issuer, tokenType, claims, ct);
    }

    /// <inheritdoc/>
    public virtual async Task<string> IssueJwtAsync(int lifetime, string issuer, string tokenType, IEnumerable<Claim> claims, CT ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenType);
        ArgumentNullException.ThrowIfNull(claims);

        var token = new Token(tokenType)
        {
            CreationTime = _timeProvider.GetUtcNow().UtcDateTime,
            Issuer = issuer,
            Lifetime = lifetime,

            Claims = new HashSet<Claim>(claims, new ClaimComparer())
        };

        return await _tokenCreation.CreateTokenAsync(token, ct);
    }

    /// <inheritdoc/>
    public virtual async Task<string> IssueClientJwtAsync(
        string clientId,
        int lifetime,
        CT ct,
        IEnumerable<string>? scopes = null,
        IEnumerable<string>? audiences = null,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var claims = new HashSet<Claim>(new ClaimComparer());

        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
            {
                claims.Add(claim);
            }
        }

        claims.Add(new Claim(JwtClaimTypes.ClientId, clientId));

        if (!IEnumerableExtensions.IsNullOrEmpty(scopes))
        {
            foreach (var scope in scopes)
            {
                claims.Add(new Claim(JwtClaimTypes.Scope, scope));
            }
        }

        if (_options.EmitStaticAudienceClaim)
        {
            claims.Add(new Claim(
                JwtClaimTypes.Audience,
#pragma warning disable CA1863 // Would require changing a public const on a public class and be a breaking change
                string.Format(CultureInfo.InvariantCulture, IdentityServerConstants.AccessTokenAudience, (await _issuerNameService.GetCurrentAsync(ct)).EnsureTrailingSlash())));
#pragma warning restore CA1863
        }

        if (!IEnumerableExtensions.IsNullOrEmpty(audiences))
        {
            foreach (var audience in audiences)
            {
                claims.Add(new Claim(JwtClaimTypes.Audience, audience));
            }
        }

        return await IssueJwtAsync(lifetime, claims, ct);
    }
}
