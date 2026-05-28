// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Provides all public keys that IdentityServer accepts for validating token signatures.
/// This includes the current signing key as well as any recently rotated keys that may
/// still be in use by previously issued tokens. The keys are published via the JWKS
/// (JSON Web Key Set) discovery endpoint so that resource servers and other parties can
/// validate tokens. Implement this interface to supply validation keys from a custom key
/// management solution.
/// </summary>
public interface IValidationKeysStore
{
    /// <summary>
    /// Gets all public keys that are currently valid for verifying token signatures.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="SecurityKeyInfo"/> objects representing the
    /// public keys and their associated signing algorithms. Returns an empty collection
    /// when no validation keys are available.
    /// </returns>
    Task<IReadOnlyCollection<SecurityKeyInfo>> GetValidationKeysAsync(Ct ct);
}
