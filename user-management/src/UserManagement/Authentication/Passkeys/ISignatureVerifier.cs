// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Verifies cryptographic signatures for WebAuthn/Passkey authentication.
/// Implement this interface to add support for additional COSE algorithms.
/// </summary>
/// <remarks>
/// Each COSE algorithm must have exactly one registered verifier.
/// Duplicate algorithm registrations, including attempts to replace a built-in verifier,
/// cause options validation to fail at host startup.
/// Verifiers are resolved once and used for the lifetime of the application, so implementations
/// must be thread-safe and should be registered as singletons.
/// </remarks>
public interface ISignatureVerifier
{
    /// <summary>
    /// Gets the COSE algorithm identifier this verifier supports.
    /// See: https://www.iana.org/assignments/cose/cose.xhtml#algorithms
    /// </summary>
    /// <remarks>
    /// Common values:
    /// <list type="bullet">
    /// <item><description>-7: ES256 (ECDSA with P-256 and SHA-256)</description></item>
    /// <item><description>-35: ES384 (ECDSA with P-384 and SHA-384)</description></item>
    /// <item><description>-36: ES512 (ECDSA with P-521 and SHA-512)</description></item>
    /// <item><description>-37: PS256 (RSASSA-PSS with SHA-256)</description></item>
    /// <item><description>-38: PS384 (RSASSA-PSS with SHA-384)</description></item>
    /// <item><description>-39: PS512 (RSASSA-PSS with SHA-512)</description></item>
    /// <item><description>-65535: RS1 (RSASSA-PKCS1-v1_5 with SHA-1, legacy TPM only)</description></item>
    /// <item><description>-257: RS256 (RSASSA-PKCS1-v1_5 with SHA-256)</description></item>
    /// <item><description>-258: RS384 (RSASSA-PKCS1-v1_5 with SHA-384)</description></item>
    /// <item><description>-259: RS512 (RSASSA-PKCS1-v1_5 with SHA-512)</description></item>
    /// </list>
    /// </remarks>
    int Algorithm { get; }

    /// <summary>
    /// Verifies a signature against the provided data using the COSE-encoded public key.
    /// </summary>
    /// <param name="publicKeyCbor">The COSE-encoded public key bytes.</param>
    /// <param name="data">
    /// The exact signed payload, before algorithm-specific hashing.
    /// For authentication assertions and packed attestation, this is authenticatorData || clientDataHash.
    /// For TPM attestation, this is certInfo.
    /// </param>
    /// <param name="signature">The signature to verify.</param>
    /// <returns>
    /// <c>true</c> if the signature is valid; otherwise, <c>false</c>.
    /// This method should not throw exceptions on validation failures.
    /// </returns>
    bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature);
}
