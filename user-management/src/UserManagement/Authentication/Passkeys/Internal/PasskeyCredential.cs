// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// A registered passkey credential (embedded in User).
/// </summary>
internal sealed record PasskeyCredential(
    PasskeyCredentialId CredentialId,
    byte[] PublicKeyCose,
    int Algorithm,
    uint SignCount,
    bool BackupEligible,
    bool BackedUp,
    Guid Aaguid,
    DateTimeOffset CreatedAt,
    string Name)
{
    internal static PasskeyCredential Create(
        TimeProvider timeProvider,
        PasskeyCredentialId credentialId,
        byte[] publicKeyCose,
        int algorithm,
        uint signCount,
        bool backupEligible,
        bool backedUp,
        Guid aaguid,
        string name) => new(
        credentialId,
        publicKeyCose,
        algorithm,
        signCount,
        backupEligible,
        backedUp,
        aaguid,
        timeProvider.GetUtcNow(),
        name);

    internal PasskeyCredential WithUpdatedSignCount(uint newSignCount) => this with { SignCount = newSignCount };

    internal PasskeyCredential WithUpdatedBackedUp(bool backedUp) => this with { BackedUp = backedUp };
}
