// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Models a token response
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// The type of access token, used to populate the token_type response parameter.
    /// </summary>
    public string AccessTokenType { get; set; } = default!;

    /// <summary>
    /// Gets or sets the identity token.
    /// </summary>
    /// <value>
    /// The identity token.
    /// </value>
    public string? IdentityToken { get; set; }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    /// <value>
    /// The access token.
    /// </value>
    public string AccessToken { get; set; } = default!;

    /// <summary>
    /// Gets or sets the access token lifetime in seconds.
    /// </summary>
    /// <value>
    /// The access token lifetime in seconds.
    /// </value>
    public int AccessTokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    /// <value>
    /// The refresh token.
    /// </value>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    /// <value>
    /// The scope.
    /// </value>
    public string Scope { get; set; } = default!;

    /// <summary>
    /// The DPoP nonce header to emit.
    /// </summary>
    public string? DPoPNonce { get; set; }

    /// <summary>
    /// Gets or sets the custom entries.
    /// </summary>
    /// <value>
    /// The custom entries.
    /// </value>
    public Dictionary<string, object> Custom { get; set; } = [];
}
