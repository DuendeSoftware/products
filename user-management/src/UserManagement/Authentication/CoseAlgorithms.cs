// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication;

/// <summary>
/// https://www.iana.org/assignments/cose/cose.xhtml#algorithms
/// </summary>
public static class CoseAlgorithms
{
    /// <summary>
    /// ECDSA with SHA-256.
    /// </summary>
    public const int Es256 = -7;

    /// <summary>
    /// ECDSA with SHA-384.
    /// </summary>
    public const int Es384 = -35;

    /// <summary>
    /// ECDSA with SHA-512.
    /// </summary>
    public const int Es512 = -36;

    /// <summary>
    /// RSASSA-PSS with SHA-256.
    /// </summary>
    public const int Ps256 = -37;

    /// <summary>
    /// RSASSA-PSS with SHA-384.
    /// </summary>
    public const int Ps384 = -38;

    /// <summary>
    /// RSASSA-PSS with SHA-512.
    /// </summary>
    public const int Ps512 = -39;

    /// <summary>
    /// RSASSA-PKCS1-v1_5 with SHA-1.
    /// SHA-1 is deprecated and this algorithm is supported only for legacy TPM attestation compatibility per RFC 8812.
    /// </summary>
    public const int Rs1 = -65535;

    /// <summary>
    /// RSASSA-PKCS1-v1_5 with SHA-256.
    /// </summary>
    public const int Rs256 = -257;

    /// <summary>
    /// RSASSA-PKCS1-v1_5 with SHA-384.
    /// </summary>
    public const int Rs384 = -258;

    /// <summary>
    /// RSASSA-PKCS1-v1_5 with SHA-512.
    /// </summary>
    public const int Rs512 = -259;
}
