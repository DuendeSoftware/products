// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Information about the user account.
/// </summary>
public sealed record PublicKeyCredentialUserEntity
{
    /// <summary>
    /// The user handle (user ID). Base64Url-encoded.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// A human-readable identifier for the user account (e.g., username or email).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A human-readable display name for the user account.
    /// </summary>
    public required string DisplayName { get; init; }
}
