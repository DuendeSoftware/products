// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Context passed to attestation trust policies for evaluation.
/// </summary>
public sealed record AttestationTrustContext
{
    /// <summary>
    /// The subject ID of the user registering the credential.
    /// Enables per-user trust decisions (e.g., "user X can only use AAGUID Y").
    /// </summary>
    public required UserSubjectId UserSubjectId { get; init; }

    /// <summary>
    /// The AAGUID of the authenticator from the attested credential data.
    /// </summary>
    public required Guid Aaguid { get; init; }

    /// <summary>
    /// The attestation format (e.g., "none", "packed").
    /// </summary>
    public required string AttestationFormat { get; init; }

    /// <summary>
    /// DER-encoded certificate bytes from the attestation statement, if present.
    /// Index 0 is the attestation certificate; remaining entries are the chain.
    /// Null when the attestation format has no certificates (e.g., "none" or self-attestation).
    /// </summary>
    public IReadOnlyList<byte[]>? CertificateChain { get; init; }
}
