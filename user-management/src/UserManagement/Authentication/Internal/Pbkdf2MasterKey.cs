// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Internal;

internal readonly struct Pbkdf2MasterKey : IEquatable<Pbkdf2MasterKey>
{
    public Pbkdf2MasterKey() => throw new InvalidOperationException();

    private Pbkdf2MasterKey(IReadOnlyCollection<byte> bytes) => Bytes = bytes;

    internal IReadOnlyCollection<byte> Bytes { get; }

    public override bool Equals(object? obj) => obj is Pbkdf2MasterKey other && Equals(other);

    public bool Equals(Pbkdf2MasterKey other) =>
        CryptographicOperations.FixedTimeEquals(Bytes.ToArray(), other.Bytes.ToArray());

    public override int GetHashCode() => Bytes.GetHashCode();

    public static bool operator ==(Pbkdf2MasterKey left, Pbkdf2MasterKey right) => left.Equals(right);

    public static bool operator !=(Pbkdf2MasterKey left, Pbkdf2MasterKey right) => !(left == right);

    public override string ToString() => GetType().ToString();

    internal static Pbkdf2MasterKey DeriveFrom(string password, Pbkdf2Inputs inputs)
    {
        var bytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            inputs.Salt.Bytes.ToArray(),
            inputs.IterationCount.Number,
            new HashAlgorithmName(inputs.PseudorandomFunctionName.Value),
            inputs.HashFunctionDigestSize.Number
        );

        return new Pbkdf2MasterKey(bytes);
    }

    internal static Pbkdf2MasterKey Load(IReadOnlyCollection<byte> bytes) => new(bytes);
}
