// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Interface for the token validator
/// </summary>
public interface ITokenValidator
{
    /// <summary>
    /// Validates an access token.
    /// </summary>
    /// <param name="token">The access token.</param>
    /// <param name="expectedScope">The expected scope.</param>
    Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string expectedScope = null);

    /// <summary>
    /// Validates an identity token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="validateLifetime">if set to <c>true</c> the lifetime gets validated. Otherwise not.</param>
    Task<TokenValidationResult> ValidateIdentityTokenAsync(string token, string clientId = null, bool validateLifetime = true);
}
