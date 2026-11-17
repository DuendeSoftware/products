// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// Builds authorization requests for scenarios that redeem their own grant at the token endpoint (design doc section
/// 09), instead of through a client app's OpenID Connect handler: a refresh token or a reference access token is never
/// exposed by <see cref="IWebApp" />'s user info endpoint, so such a scenario drives <c>/connect/authorize</c> itself
/// with <see cref="TestBrowser.AuthorizeAsync" />.
/// </summary>
public static class AuthorizationRequests
{
    /// <summary>
    /// Builds a PKCE authorization code request's URL for <paramref name="client" /> at <paramref name="issuer" />.
    /// <paramref name="redirectUri" /> should be one the client is allowed to use that nothing maps (for example
    /// <see cref="IWebApp.ManualCallbackUrl" />), so <see cref="TestBrowser.AuthorizeAsync" /> stops there with the code
    /// visible in its query, unscrubbed.
    /// </summary>
    public static Uri CodeFlowUrl(this IIssuer issuer, Client client, string scope, Uri redirectUri, string state, string codeChallenge)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(redirectUri);

        var baseUrl = $"{issuer.Url.GetLeftPart(UriPartial.Path).TrimEnd('/')}/connect/authorize";
        return new Uri(QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>
        {
            ["client_id"] = client.ClientId,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["redirect_uri"] = redirectUri.ToString(),
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        }));
    }
}

/// <summary>Generates PKCE (RFC 7636) code verifiers and challenges for a scenario that redeems its own authorization code.</summary>
public static class Pkce
{
    /// <summary>A new random code verifier, long enough for every code challenge method IdentityServer supports.</summary>
    public static string NewCodeVerifier() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 code challenge for <paramref name="codeVerifier" />.</summary>
    public static string Challenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }
}
