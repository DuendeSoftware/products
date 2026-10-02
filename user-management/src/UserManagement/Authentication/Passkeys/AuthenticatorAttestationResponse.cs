// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// The attestation response from the authenticator.
/// </summary>
public sealed class AuthenticatorAttestationResponse
{
    /// <summary>
    /// The client data JSON (base64url-encoded).
    /// </summary>
    public required string ClientDataJSON { get; init; }

    /// <summary>
    /// The attestation object (base64url-encoded CBOR).
    /// </summary>
    public required string AttestationObject { get; init; }

    /// <summary>
    /// Optional transport hints reported by the client via getTransports().
    /// These values guide browser UI and are not verified authenticator properties.
    /// </summary>
    public IReadOnlyList<string>? Transports { get; init; }
}
