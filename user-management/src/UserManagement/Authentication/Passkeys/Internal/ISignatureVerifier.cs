// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Verifies cryptographic signatures for WebAuthn/Passkey authentication.
/// Implement this interface to add support for additional COSE algorithms.
/// </summary>
internal interface ISignatureVerifier
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
    /// <param name="data">The signed data (authenticatorData || clientDataHash).</param>
    /// <param name="signature">The signature to verify.</param>
    /// <returns>
    /// <c>true</c> if the signature is valid; otherwise, <c>false</c>.
    /// This method should not throw exceptions on validation failures.
    /// </returns>
    bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature);
}
