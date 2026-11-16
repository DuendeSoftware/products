// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer;

/// <summary>
/// A <see cref="JsonWebTokenHandler"/> whose
/// <see cref="ValidateTokenAsync(string, TokenValidationParameters)"/> never reads or writes the process-wide cache of
/// signature verifiers. The other validation methods it inherits still use that cache.
/// </summary>
/// <remarks>
/// <para>
/// By default, Microsoft.IdentityModel keeps one process-wide cache of signature verifiers in
/// <see cref="CryptoProviderFactory.Default"/>. An entry is found by the key type, the key's
/// <c>InternalId</c>, the algorithm and the provider type. For a <see cref="JsonWebKey"/>, the
/// internal ID is the RFC 7638 thumbprint of the public key members only. But an RSA <see cref="JsonWebKey"/> that
/// also has an <c>x5c</c> member is verified with the key of that certificate, and the library does not check that the
/// two keys match. The cache adds an entry only when there is none, and keeps it until the cache trims it.
/// </para>
/// <para>
/// So one check with such a key (a DPoP proof, a client assertion, a request object, or code outside our products)
/// decides the result of later checks in the process that use a <see cref="JsonWebKey"/> with the same public key
/// members and the same algorithm.
/// </para>
/// <para>
/// This handler validates on a copy of the parameters, with its own <see cref="CryptoProviderFactory"/> that does not
/// cache signature verifiers. Each check builds its verifier from the key it is given and adds nothing to the shared
/// cache. Any <see cref="TokenValidationParameters.CryptoProviderFactory"/> set by the caller, and the key's own
/// <see cref="SecurityKey.CryptoProviderFactory"/>, are not used. It does not change how <c>x5c</c> is used inside one
/// check.
/// </para>
/// </remarks>
internal sealed class NonCachedJsonWebTokenHandler : JsonWebTokenHandler
{
    private static readonly CryptoProviderFactory NonCachingCryptoProviderFactory = new()
    {
        CacheSignatureProviders = false
    };

    public override Task<TokenValidationResult> ValidateTokenAsync(string token, TokenValidationParameters validationParameters)
    {
        var nonCachingParameters = validationParameters.Clone();
        nonCachingParameters.CryptoProviderFactory = NonCachingCryptoProviderFactory;
        return base.ValidateTokenAsync(token, nonCachingParameters);
    }
}
