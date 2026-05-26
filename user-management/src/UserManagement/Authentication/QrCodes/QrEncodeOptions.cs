// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes;

/// <summary>
/// Configuration options for encoding a QR code symbol.
/// </summary>
public sealed class QrEncodeOptions
{
    /// <summary>
    /// The error correction level to use when encoding the QR code.
    /// Defaults to <see cref="QrEccLevel.M"/> (~15% recovery capacity),
    /// which is a sensible general-purpose choice.
    /// </summary>
    public QrEccLevel EccLevel { get; set; } = QrEccLevel.M;

    /// <summary>
    /// An optional QR version override (1-40). When <see langword="null"/>,
    /// the encoder automatically selects the smallest version that fits the data.
    /// When set, the encoder uses exactly this version and throws if the data does not fit.
    /// </summary>
    public int? Version { get; set; }
}
