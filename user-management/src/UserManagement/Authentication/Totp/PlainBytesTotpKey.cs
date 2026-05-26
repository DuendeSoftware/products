// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Totp.Internal;

namespace Duende.UserManagement.Authentication.Totp;

public readonly record struct PlainBytesTotpKey
{
    // the message digest size of SHA-1 is 160 bits (20 bytes)
    // - https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.180-4.pdf section 1
    private const int Length = 20;

    public PlainBytesTotpKey() => throw new InvalidOperationException();

    private PlainBytesTotpKey(IReadOnlyCollection<byte> bytes) => Bytes = bytes;

    internal IReadOnlyCollection<byte> Bytes { get; }

    public string EncodeToBase32() => Base32.Encode(Bytes);

    public IReadOnlyCollection<string> EncodeToBase32Groups() => [.. EncodeToBase32().ToGroups()];

    public override string ToString() => GetType().ToString();

    public static PlainBytesTotpKey New() => new(RandomNumberGenerator.GetBytes(Length));

    public static PlainBytesTotpKey DecodeFromBase32(string input) =>
        TryDecodeFromBase32(input, out var result) ? result.Value : throw new FormatException();

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
