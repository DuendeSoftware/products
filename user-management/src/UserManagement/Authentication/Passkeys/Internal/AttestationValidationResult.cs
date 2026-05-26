// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Result of attestation validation.
/// </summary>
internal abstract record AttestationValidationResult
{
    private AttestationValidationResult() { }

    /// <summary>
    /// The attestation statement was validated successfully.
    /// </summary>
    /// <param name="CertificateChain">
    /// DER-encoded certificate bytes from the attestation statement, if present.
    /// Index 0 is the attestation certificate; remaining entries are the chain.
    /// Null when the attestation format has no certificates (e.g., "none" or self-attestation).
    /// </param>
    internal sealed record Success(IReadOnlyList<byte[]>? CertificateChain) : AttestationValidationResult;

    /// <summary>
    /// The attestation statement failed validation.
    /// </summary>
    /// <param name="Error">The type of validation error.</param>
    /// <param name="ErrorDescription">A human-readable description of the failure.</param>
    internal sealed record Failure(AttestationValidationError Error, string ErrorDescription) : AttestationValidationResult;
}
