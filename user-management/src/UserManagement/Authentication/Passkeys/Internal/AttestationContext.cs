// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Context for attestation validation, containing the attestation statement,
/// authenticator data, client data hash, and credential public key.
/// </summary>
internal sealed record AttestationContext(
    IReadOnlyDictionary<string, object?> AttStmt,
    IReadOnlyCollection<byte> AuthData,
    IReadOnlyCollection<byte> ClientDataHash,
    CoseKey CredentialPublicKey);
