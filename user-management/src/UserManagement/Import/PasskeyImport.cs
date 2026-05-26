// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// A passkey credential to import.
/// </summary>
public sealed record PasskeyImport
{
    /// <summary>The credential ID bytes from the WebAuthn authenticator.</summary>
    public required IReadOnlyList<byte> CredentialId { get; init; }

    /// <summary>The COSE-encoded public key.</summary>
    public required IReadOnlyList<byte> PublicKeyCose { get; init; }

    /// <summary>The COSE algorithm identifier (e.g., -7 for ES256).</summary>
    public required int Algorithm { get; init; }

    /// <summary>The signature counter value at time of import.</summary>
    public uint SignCount { get; init; }

    /// <summary>Whether the credential is eligible for backup.</summary>
    public bool BackupEligible { get; init; }

    /// <summary>Whether the credential is currently backed up.</summary>
    public bool BackedUp { get; init; }

    /// <summary>The AAGUID of the authenticator device.</summary>
    public Guid Aaguid { get; init; }

    /// <summary>A human-readable name for the passkey.</summary>
    public required string Name { get; init; }
}
