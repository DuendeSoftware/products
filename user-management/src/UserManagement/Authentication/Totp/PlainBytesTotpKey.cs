// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Totp.Internal;

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Represents the raw byte secret key used to generate TOTP codes.
/// </summary>
public readonly record struct PlainBytesTotpKey
{
    // the message digest size of SHA-1 is 160 bits (20 bytes)
    // - https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.180-4.pdf section 1
    private const int Length = 20;

    /// <summary>Disallow default construction.</summary>
    public PlainBytesTotpKey() => throw new InvalidOperationException();

    private PlainBytesTotpKey(IReadOnlyCollection<byte> bytes) => Bytes = bytes;

    internal IReadOnlyCollection<byte> Bytes { get; }

    /// <summary>Encodes the key bytes as a Base32 string.</summary>
    public string EncodeToBase32() => Base32.Encode(Bytes);

    /// <summary>Encodes the key bytes as a collection of Base32 display groups.</summary>
    public IReadOnlyCollection<string> EncodeToBase32Groups() => [.. EncodeToBase32().ToGroups()];

    /// <summary>Returns a type-safe string representation (does not expose the key value).</summary>
    public override string ToString() => GetType().ToString();

    /// <summary>Generates a new random TOTP key.</summary>
    public static PlainBytesTotpKey New() => new(RandomNumberGenerator.GetBytes(Length));

    /// <summary>
    /// Decodes a TOTP key from a Base32 string.
    /// Throws <see cref="FormatException"/> if the input is invalid.
    /// </summary>
    /// <param name="input">The Base32-encoded key string.</param>
    public static PlainBytesTotpKey DecodeFromBase32(string input) =>
        TryDecodeFromBase32(input, out var result) ? result.Value : throw new FormatException();

    /// <summary>
    /// Attempts to decode a TOTP key from a Base32 string.
    /// </summary>
    /// <param name="input">The Base32-encoded key string.</param>
    /// <param name="result">The decoded key if successful.</param>
    public static bool TryDecodeFromBase32(string input, [NotNullWhen(true)] out PlainBytesTotpKey? result)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Base32.TryDecode(input, out var bytes))
        {
            result = new PlainBytesTotpKey(bytes);
            return true;
        }

        result = null;
        return false;
    }

    internal static PlainBytesTotpKey Load(IReadOnlyCollection<byte> bytes) => new(bytes);
}
