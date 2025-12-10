// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Detects replay of proof tokens.
/// </summary>
public interface IReplayCache
{
    /// <summary>
    /// Adds a hashed jti to the cache.
    /// </summary>
    Task Add(string jtiHash, TimeSpan expiration, CancellationToken cancellationToken = default);


    /// <summary>
    /// Checks if a cached jti hash exists in the hash.
    /// </summary>
    Task<bool> Exists(string jtiHash, CancellationToken cancellationToken = default);
}
