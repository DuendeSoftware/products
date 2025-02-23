// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Models a token revocation response
/// </summary>
public class TokenRevocationResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the token revocation was successful.
    /// </summary>
    /// <value>
    ///   <c>true</c> if success; otherwise, <c>false</c>.
    /// </value>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the type of the token that was revoked.
    /// </summary>
    /// <value>
    /// The type of the token.
    /// </value>
    public string TokenType { get; set; }

    /// <summary>
    /// Gets or sets an error (if present).
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public string Error { get; set; }
}