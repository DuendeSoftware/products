// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Passkey credential data returned from registration completion.
/// </summary>
public sealed record PasskeyCredentialData(
    PasskeyCredentialId CredentialId,
    IReadOnlyCollection<byte> PublicKeyCose,
    int Algorithm,
    uint SignCount,
    bool BackupEligible,
    bool BackedUp,
    Guid Aaguid,
    DateTimeOffset CreatedAt,
    string Name);
