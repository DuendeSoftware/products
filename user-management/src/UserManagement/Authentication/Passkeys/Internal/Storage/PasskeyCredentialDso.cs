// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

/// <summary>
/// Embedded passkey credential storage (within UserAuthenticatorsDso).
/// </summary>
/// <remarks>
/// Public keys are stored unencrypted, unlike TOTP keys which use Data Protection.
/// This is correct: public keys are not secrets and cannot be used to sign anything,
/// only to verify signatures. Encrypting them would provide no additional security.
/// </remarks>
internal static class PasskeyCredentialDso
{
    internal sealed record V1(
        byte[] CredentialId,
        byte[] PublicKeyCose,
        int Algorithm,
        uint SignCount,
        bool BackupEligible,
        bool BackedUp,
        Guid Aaguid,
        DateTimeOffset CreatedAt,
        string Name);
}
