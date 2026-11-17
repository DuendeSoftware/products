// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Information about a registered passkey for display in management UIs.
/// </summary>
public sealed record UserPasskey(
    PasskeyCredentialId CredentialId,
    string Name,
    DateTimeOffset CreatedAt);
