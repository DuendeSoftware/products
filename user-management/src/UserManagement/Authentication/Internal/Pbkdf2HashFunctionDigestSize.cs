// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal readonly record struct Pbkdf2HashFunctionDigestSize
{
    // the message digest size of SHA-512 is 512 bits (64 bytes)
    // - https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.180-4.pdf section 1
    private const int Sha512HashFunctionDigestSize = 64;

    public Pbkdf2HashFunctionDigestSize() => throw new InvalidOperationException();

    private Pbkdf2HashFunctionDigestSize(int number) => Number = number;

    internal int Number { get; }

    internal static Pbkdf2HashFunctionDigestSize For(Pbkdf2PseudorandomFunctionName pseudorandomFunctionName) =>
        pseudorandomFunctionName.Equals(Pbkdf2PseudorandomFunctionName.Sha512)
            ? new Pbkdf2HashFunctionDigestSize(Sha512HashFunctionDigestSize)
            : throw new ArgumentOutOfRangeException(nameof(pseudorandomFunctionName));

    public override string ToString() => GetType().ToString();

    internal static Pbkdf2HashFunctionDigestSize Load(int number) => new(number);
}
