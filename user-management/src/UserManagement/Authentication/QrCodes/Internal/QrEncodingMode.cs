// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// The data encoding mode used to encode a segment of input data in a QR code symbol.
/// Kanji encoding is intentionally omitted; arbitrary input falls back to <see cref="Byte"/>.
/// </summary>
internal enum QrEncodingMode
{
    /// <summary>
    /// Numeric mode - encodes digits 0-9 with the highest data density.
    /// </summary>
    Numeric,

    /// <summary>
    /// Alphanumeric mode - encodes digits, uppercase A-Z, space, and the symbols $%*+-./:
    /// </summary>
    Alphanumeric,

    /// <summary>
    /// Byte mode - encodes arbitrary byte data (ISO 8859-1 or UTF-8).
    /// </summary>
    Byte,
}
