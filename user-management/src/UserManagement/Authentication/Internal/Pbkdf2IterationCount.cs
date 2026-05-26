// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal readonly record struct Pbkdf2IterationCount
{
    // https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2
    private const int Sha512 = 210_000;

    public Pbkdf2IterationCount() => throw new InvalidOperationException();

    private Pbkdf2IterationCount(int number) => Number = number;

    internal int Number { get; }

    internal static Pbkdf2IterationCount For(Pbkdf2PseudorandomFunctionName pseudorandomFunctionName) =>
        pseudorandomFunctionName.Equals(Pbkdf2PseudorandomFunctionName.Sha512)
            ? new Pbkdf2IterationCount(Sha512)
            : throw new ArgumentOutOfRangeException(nameof(pseudorandomFunctionName));

    public override string ToString() => GetType().ToString();

    internal static Pbkdf2IterationCount Load(int number) => new(number);
}
