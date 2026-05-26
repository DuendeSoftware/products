// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes;

/// <summary>
/// The error correction level for a QR code symbol, as defined in ISO/IEC 18004.
/// Higher levels can recover from more damage but reduce data capacity.
/// </summary>
public enum QrEccLevel
{
    /// <summary>
    /// Low - approximately 7% of codewords can be restored.
    /// </summary>
    L,

    /// <summary>
    /// Medium - approximately 15% of codewords can be restored.
    /// </summary>
    M,

    /// <summary>
    /// Quartile - approximately 25% of codewords can be restored.
    /// </summary>
    Q,

    /// <summary>
    /// High - approximately 30% of codewords can be restored.
    /// </summary>
    H
}
