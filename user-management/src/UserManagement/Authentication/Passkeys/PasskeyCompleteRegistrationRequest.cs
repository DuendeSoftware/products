// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Request to complete passkey registration with the authenticator's attestation response.
/// </summary>
public sealed class PasskeyCompleteRegistrationRequest
{
    /// <summary>
    /// The Challenge ID returned from BeginAsync.
    /// </summary>
    public required Guid ChallengeId { get; init; }

    /// <summary>
    /// The credential ID (base64url-encoded).
    /// https://w3c.github.io/webappsec-credential-management/#dom-credential-id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The raw credential ID (base64url-encoded).
    /// https://w3c.github.io/webauthn/#dom-publickeycredential-rawid
    /// </summary>
    public required string RawId { get; init; }

    /// <summary>
    /// The credential type. Must be "public-key".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The authenticator's attestation response.
    /// </summary>
    public required AuthenticatorAttestationResponse Response { get; init; }

    /// <summary>
    /// The name of the authenticator. Required and must be unique per user.
    /// </summary>
    public required string Name { get; init; }
}
