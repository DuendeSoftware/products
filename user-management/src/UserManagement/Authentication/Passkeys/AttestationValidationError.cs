// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Describes the type of attestation validation failure.
/// </summary>
public enum AttestationValidationError
{
    /// <summary>The attestation statement is invalid.</summary>
    InvalidAttestationStatement,
    /// <summary>The algorithm in the statement does not match the credential key.</summary>
    AlgorithmMismatch,
    /// <summary>The algorithm is not supported.</summary>
    UnsupportedAlgorithm,
    /// <summary>The attestation signature could not be verified.</summary>
    SignatureVerificationFailed,
    /// <summary>A certificate in the attestation chain is invalid.</summary>
    InvalidCertificate,
    /// <summary>The certificate AAGUID does not match the authenticator data.</summary>
    AaguidMismatch
}
