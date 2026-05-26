// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Constants for WebAuthn authenticator data layout and well-known OIDs.
/// </summary>
internal static class WebAuthnConstants
{
    /// <summary>
    /// Byte layout of the authenticator data structure.
    /// See: https://www.w3.org/TR/webauthn-3/#sctn-authenticator-data (§6.5.1)
    /// </summary>
    internal static class AuthenticatorDataLayout
    {
        /// <summary>
        /// Length of the RP ID hash (SHA-256) in bytes.
        /// </summary>
        internal const int RpIdHashLength = 32;

        /// <summary>
        /// Length of the flags byte.
        /// </summary>
        internal const int FlagsLength = 1;

        /// <summary>
        /// Length of the signature counter in bytes.
        /// </summary>
        internal const int SignCountLength = 4;

        /// <summary>
        /// Total length of the fixed authenticator data header
        /// (rpIdHash + flags + signCount).
        /// </summary>
        internal const int HeaderLength = RpIdHashLength + FlagsLength + SignCountLength;

        /// <summary>
        /// Length of the AAGUID in bytes.
        /// </summary>
        internal const int AaguidLength = 16;
    }

    /// <summary>
    /// Well-known OIDs used in WebAuthn and FIDO attestation.
    /// </summary>
    internal static class Oids
    {
        /// <summary>
        /// EC public key OID (id-ecPublicKey).
        /// https://datatracker.ietf.org/doc/draft-ietf-cose-cbor-encoded-cert/03/
        /// </summary>
        internal const string EcPublicKey = "1.2.840.10045.2.1";

        /// <summary>
        /// RSA encryption OID (rsaEncryption).
        /// https://datatracker.ietf.org/doc/draft-ietf-cose-cbor-encoded-cert/03/
        /// </summary>
        internal const string RsaEncryption = "1.2.840.113549.1.1.1";

        /// <summary>
        /// FIDO AAGUID extension OID for attestation certificates.
        /// See: https://www.w3.org/TR/webauthn-3/#sctn-packed-attestation (§8.2)
        /// </summary>
        internal const string FidoAaguidExtension = "1.3.6.1.4.1.45724.1.1.4";

        /// <summary>
        /// TCG AIK certificate extended key usage OID for TPM attestation certificates.
        /// See: https://www.w3.org/TR/webauthn-3/#sctn-tpm-attestation (§8.3)
        /// </summary>
        internal const string TcgKpAikCertificate = "2.23.133.8.3";
    }
}
