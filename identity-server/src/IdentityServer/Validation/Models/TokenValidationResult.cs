// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models the validation result of access tokens and id tokens.
/// </summary>
public class TokenValidationResult : ValidationResult
{
    /// <summary>
    /// Gets or sets the claims.
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public IEnumerable<Claim>? Claims { get; set; }

    /// <summary>
    /// Gets or sets the JWT.
    /// </summary>
    /// <value>
    /// The JWT.
    /// </value>
    public string? Jwt { get; set; }

    /// <summary>
    /// Gets or sets the reference token (in case of access token validation).
    /// </summary>
    /// <value>
    /// The reference token.
    /// </value>
    public Token? ReferenceToken { get; set; }

    /// <summary>
    /// Gets or sets the reference token identifier (in case of access token validation).
    /// </summary>
    /// <value>
    /// The reference token identifier.
    /// </value>
    public string? ReferenceTokenId { get; set; }

    /// <summary>
    /// Gets or sets the refresh token (in case of refresh token validation).
    /// </summary>
    /// <value>
    /// The reference token identifier.
    /// </value>
    public RefreshToken? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    public Client? Client { get; set; }
}
