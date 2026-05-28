// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Responsible for serializing a <see cref="Token"/> model into a signed and protected JWT string.
/// The default implementation is <c>DefaultTokenCreationService</c>.
/// This is the lowest-level token creation hook; prefer <see cref="IProfileService"/>,
/// <c>IClaimsService</c>, or <see cref="ITokenService"/> for adding or modifying claims,
/// and only implement this interface when those extension points are insufficient.
/// If customization is needed, derive from <c>DefaultTokenCreationService</c> and override
/// <c>CreatePayloadAsync</c> rather than implementing this interface from scratch.
/// </summary>
public interface ITokenCreationService
{
    /// <summary>
    /// Converts the given <see cref="Token"/> model into a signed and serialized JWT string.
    /// This is the final step in token creation and provides a last opportunity to modify
    /// the token's payload (e.g. add audiences or claims) before it is signed.
    /// </summary>
    /// <param name="token">The token model describing the claims, lifetime, and signing key to use.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A compact, signed JWT string ready to be returned to the client.
    /// </returns>
    Task<string> CreateTokenAsync(Token token, Ct ct);
}
