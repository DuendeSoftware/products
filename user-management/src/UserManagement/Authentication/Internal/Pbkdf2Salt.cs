// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Internal;

internal readonly record struct Pbkdf2Salt
{
    // "The length of the randomly-generated portion of the salt shall be at least 128 bits (16 bytes)."
    // - https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-132.pdf section 5.1
    private const int Length = 16;

    public Pbkdf2Salt() => throw new InvalidOperationException();

    private Pbkdf2Salt(IReadOnlyCollection<byte> bytes) => Bytes = bytes;

    internal IReadOnlyCollection<byte> Bytes { get; }

    internal static Pbkdf2Salt New() => new(RandomNumberGenerator.GetBytes(Length));

    public override string ToString() => GetType().ToString();

    internal static Pbkdf2Salt Load(IReadOnlyCollection<byte> bytes) => new(bytes);
}
