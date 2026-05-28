// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Handles serialization and deserialization of <see cref="SamlAuthenticationState"/>
/// for Entity Framework storage. Replace the default implementation to customize
/// how SAML authentication state is persisted (e.g., to support custom Extensions content).
/// </summary>
public interface ISamlSigninStateSerializer
{
    /// <summary>
    /// Serializes a <see cref="SamlAuthenticationState"/> to a string for storage.
    /// </summary>
    /// <param name="state">The SAML authentication state to serialize.</param>
    /// <returns>The serialized string representation.</returns>
    string Serialize(SamlAuthenticationState state);

    /// <summary>
    /// Deserializes a <see cref="SamlAuthenticationState"/> from its stored string representation.
    /// </summary>
    /// <param name="serializedState">The serialized state string.</param>
    /// <returns>The deserialized SAML authentication state, or <see langword="null"/> if deserialization fails.</returns>
    SamlAuthenticationState? Deserialize(string serializedState);
}
