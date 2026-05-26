// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Describes the type of attestation validation failure.
/// </summary>
internal enum AttestationValidationError
{
    InvalidAttestationStatement,
    AlgorithmMismatch,
    UnsupportedAlgorithm,
    SignatureVerificationFailed,
    InvalidCertificate,
    AaguidMismatch
}
