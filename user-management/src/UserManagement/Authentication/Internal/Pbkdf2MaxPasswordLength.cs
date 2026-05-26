// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal static class Pbkdf2MaxPasswordLength
{
    // limit the password length to 1024 bits (128 bytes) to avoid pre-hashing by PBKDF2 or parsing by SHA-512
    // - https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2-pre-hashing
    // - https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.180-4.pdf section 5.2.2
    // a .NET string "Represents text as a sequence of UTF-16 code units." and therefore occupies 2 bytes per character
    // - https://learn.microsoft.com/en-us/dotnet/api/system.string
    private const byte Sha512 = 64;

    internal static byte For(Pbkdf2PseudorandomFunctionName pseudorandomFunctionName) =>
        pseudorandomFunctionName.Equals(Pbkdf2PseudorandomFunctionName.Sha512)
            ? Sha512
            : throw new ArgumentOutOfRangeException(nameof(pseudorandomFunctionName));
}
