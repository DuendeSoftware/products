// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models a validated request to the token endpoint.
/// </summary>
public class ValidatedTokenRequest : ValidatedRequest
{
    /// <summary>
    /// Gets or sets the type of the grant.
    /// </summary>
    /// <value>
    /// The type of the grant.
    /// </value>
    public string GrantType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the scopes.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public IEnumerable<string>? RequestedScopes { get; set; }

    /// <summary>
    /// Gets or sets the resource indicator.
    /// </summary>
    public string? RequestedResourceIndicator { get; set; }

    /// <summary>
    /// Gets or sets the username used in the request.
    /// </summary>
    /// <value>
    /// The name of the user.
    /// </value>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    /// <value>
    /// The refresh token.
    /// </value>
    public RefreshToken? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token handle.
    /// </summary>
    /// <value>
    /// The refresh token handle.
    /// </value>
    public string? RefreshTokenHandle { get; set; }

    /// <summary>
    /// Gets or sets the authorization code.
    /// </summary>
    /// <value>
    /// The authorization code.
    /// </value>
    public AuthorizationCode? AuthorizationCode { get; set; }

    /// <summary>
    /// Gets or sets the authorization code handle.
    /// </summary>
    /// <value>
    /// The authorization code handle.
    /// </value>
    public string? AuthorizationCodeHandle { get; set; }

    /// <summary>
    /// Gets or sets the code verifier.
    /// </summary>
    /// <value>
    /// The code verifier.
    /// </value>
    public string? CodeVerifier { get; set; }

    /// <summary>
    /// Gets or sets the device code.
    /// </summary>
    /// <value>
    /// The device code.
    /// </value>
    public DeviceCode? DeviceCode { get; set; }

    /// <summary>
    /// Gets or sets the backchannel authentication request.
    /// </summary>
    /// <value>
    /// The backchannel authentication request.
    /// </value>
    public BackChannelAuthenticationRequest? BackChannelAuthenticationRequest { get; set; }

    /// <summary>
    /// The thumbprint of the associated proof key, if one was used.
    /// </summary>
    public string? ProofKeyThumbprint { get; set; }
}
