// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Information about the relying party.
/// </summary>
public sealed record PublicKeyCredentialRelyingPartyEntity
{
    /// <summary>
    /// The relying party identifier (RP ID).
    /// If omitted, defaults to the origin's effective domain.
    /// </summary>
    /// <remarks>
    /// See <see href="https://www.w3.org/TR/webauthn-3/#rp-id"/>.
    /// </remarks>
    public string? Id { get; init; }

    /// <summary>
    /// A human-readable name for the relying party.
    /// </summary>
    public required string Name { get; init; }
}
