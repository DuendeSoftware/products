// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Internal;

[StringValue]
internal partial record Pbkdf2PseudorandomFunctionName
{
    public string Value { get; }

    internal Pbkdf2PseudorandomFunctionName(string value) => Value = value;

    internal static Pbkdf2PseudorandomFunctionName Sha512 => new(HashAlgorithmName.SHA512.Name!);

    // SHA-512 is more secure than SHA-256
    // - https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-57pt1r5.pdf section 5.6.1.2
    internal static Pbkdf2PseudorandomFunctionName Default => Sha512;

    public override string ToString() => GetType().ToString();

    internal static Pbkdf2PseudorandomFunctionName Load(string value) => new(value);
}
