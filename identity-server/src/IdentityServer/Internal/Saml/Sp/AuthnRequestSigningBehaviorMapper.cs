// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Internal.Saml.Sp;

/// <summary>
/// Maps the public <see cref="AuthnRequestSigningBehavior" /> enum to the internal
/// <see cref="SigningBehavior" /> enum used by the underlying SAML SP implementation.
/// </summary>
internal static class AuthnRequestSigningBehaviorMapper
{
    /// <summary>
    /// Maps a public <see cref="AuthnRequestSigningBehavior" /> value to the corresponding
    /// internal <see cref="SigningBehavior" /> value.
    /// </summary>
    /// <param name="behavior">The public signing behavior value.</param>
    /// <returns>The corresponding internal signing behavior value.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="behavior" /> is not a recognized value.
    /// </exception>
    public static SigningBehavior Map(AuthnRequestSigningBehavior behavior) => behavior switch
    {
        AuthnRequestSigningBehavior.Always => SigningBehavior.Always,
        AuthnRequestSigningBehavior.Never => SigningBehavior.Never,
        _ => throw new InvalidOperationException(
            $"Unrecognized {nameof(AuthnRequestSigningBehavior)} value '{behavior}'.")
    };
}
