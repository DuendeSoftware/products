// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Constants for COSE (CBOR Object Signing and Encryption) as defined in RFC 9053.
/// Used for WebAuthn/Passkey credential parsing and signature verification.
/// </summary>
internal static class CoseConstants
{
    /// <summary>
    /// COSE key type identifiers.
    /// See: https://www.iana.org/assignments/cose/cose.xhtml#key-type
    /// </summary>
    public static class KeyTypes
    {
        /// <summary>
        /// Elliptic Curve Keys with x- and y-coordinate pair
        /// </summary>
        public const int Ec2 = 2;

        /// <summary>
        /// RSA Keys
        /// </summary>
        public const int Rsa = 3;
    }

    /// <summary>
    /// COSE key map labels (common and algorithm-specific parameters).
    /// See: https://www.iana.org/assignments/cose/cose.xhtml#key-common-parameters
    /// </summary>
    public static class Labels
    {
        /// <summary>
        /// Key type identification (common parameter)
        /// </summary>
        public const int KeyType = 1;

        /// <summary>
        /// Key usage restriction to this algorithm (common parameter)
        /// </summary>
        public const int Algorithm = 3;

        /// <summary>
        /// EC2: Elliptic Curve identifier
        /// </summary>
        public const int EcCurve = -1;

        /// <summary>
        /// EC2: x-coordinate
        /// </summary>
        public const int EcX = -2;

        /// <summary>
        /// EC2: y-coordinate
        /// </summary>
        public const int EcY = -3;

        /// <summary>
        /// RSA: Modulus n
        /// </summary>
        public const int RsaModulus = -1;

        /// <summary>
        /// RSA: Public exponent e
        /// </summary>
        public const int RsaExponent = -2;
    }

    /// <summary>
    /// COSE elliptic curve identifiers and parameters.
    /// See: https://www.iana.org/assignments/cose/cose.xhtml#elliptic-curves
    /// </summary>
    public static class Curves
    {
        /// <summary>
        /// NIST P-256 (secp256r1) curve identifier
        /// </summary>
        public const int P256 = 1;

        /// <summary>
        /// NIST P-384 (secp384r1) curve identifier
        /// </summary>
        public const int P384 = 2;

        /// <summary>
        /// NIST P-521 (secp521r1) curve identifier
        /// </summary>
        public const int P521 = 3;

        /// <summary>
        /// Expected coordinate length in bytes for P-256 curve
        /// </summary>
        public const int P256CoordinateLength = 32;

        /// <summary>
        /// Expected coordinate length in bytes for P-384 curve
        /// </summary>
        public const int P384CoordinateLength = 48;

        /// <summary>
        /// Expected coordinate length in bytes for P-521 curve
        /// </summary>
        public const int P521CoordinateLength = 66;
    }
}
