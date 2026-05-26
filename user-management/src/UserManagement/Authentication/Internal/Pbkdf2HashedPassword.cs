// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

// PBKDF2 isn't the best method.
// See https://security.stackexchange.com/questions/216380/which-is-the-best-password-hashing-algorithm-in-net-core#216381
// However, currently, only PBKDF2 is supported by the .NET SDK.
// There are third party packages for Argon2, bcrypt, and scrypt (nothing for Catena).
// For now, we are using PBKDF2.
// Later we may consider adding plug-in packages which reference the third party packages,
// or wait until a better algorithm is added to the .NET SDK.
internal sealed record Pbkdf2HashedPassword
{
    private Pbkdf2HashedPassword(Pbkdf2Inputs inputs, Pbkdf2MasterKey masterKey)
    {
        Inputs = inputs;
        MasterKey = masterKey;
    }

    internal Pbkdf2Inputs Inputs { get; }

    internal Pbkdf2MasterKey MasterKey { get; }

    internal static Pbkdf2HashedPassword Load(Pbkdf2Inputs inputs, Pbkdf2MasterKey masterKey) => new(inputs, masterKey);

    internal static Pbkdf2HashedPassword From(string password)
    {
        var inputs = new Pbkdf2Inputs();
        var masterKey = Pbkdf2MasterKey.DeriveFrom(password, inputs);
        return new Pbkdf2HashedPassword(inputs, masterKey);
    }
}
