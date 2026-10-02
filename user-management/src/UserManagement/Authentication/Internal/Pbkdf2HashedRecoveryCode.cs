// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal sealed record Pbkdf2HashedRecoveryCode
{
    private Pbkdf2HashedRecoveryCode(Pbkdf2Inputs inputs, Pbkdf2MasterKey masterKey)
    {
        Inputs = inputs;
        MasterKey = masterKey;
    }

    internal Pbkdf2Inputs Inputs { get; }

    internal Pbkdf2MasterKey MasterKey { get; }

    internal static Pbkdf2HashedRecoveryCode Load(Pbkdf2Inputs inputs, Pbkdf2MasterKey masterKey) => new(inputs, masterKey);

    internal static Pbkdf2HashedRecoveryCode From(string recoveryCode)
    {
        var inputs = new Pbkdf2Inputs();
        var masterKey = Pbkdf2MasterKey.DeriveFrom(recoveryCode, inputs);
        return new Pbkdf2HashedRecoveryCode(inputs, masterKey);
    }
}
