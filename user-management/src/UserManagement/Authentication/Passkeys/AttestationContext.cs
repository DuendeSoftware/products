// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Context for attestation validation, containing the attestation statement,
/// authenticator data, client data hash, and credential public key.
/// </summary>
/// <param name="AttStmt">The attestation statement values.</param>
/// <param name="AuthData">The authenticator data bytes.</param>
/// <param name="ClientDataHash">The SHA-256 hash of the client data JSON bytes.</param>
/// <param name="CredentialPublicKeyCbor">The credential public key encoded as COSE CBOR.</param>
/// <remarks>
/// In <see cref="AttStmt"/>, CBOR text strings are <c>string</c>, byte strings are <c>byte[]</c>,
/// integers are <c>long</c>, and booleans are <c>bool</c>. Integer values must fit in a <c>long</c>.
/// Arrays are <c>object?[]</c> and maps are <c>Dictionary&lt;string, object?&gt;</c>,
/// with these mappings applied recursively. Map keys must be text strings.
/// CBOR null is represented as <see langword="null"/>.
/// Unsupported values are skipped and represented as <see langword="null"/>.
/// </remarks>
public sealed record AttestationContext(
    IReadOnlyDictionary<string, object?> AttStmt,
    IReadOnlyCollection<byte> AuthData,
    IReadOnlyCollection<byte> ClientDataHash,
    IReadOnlyCollection<byte> CredentialPublicKeyCbor);
