// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Authenticator data flags.
/// </summary>
[Flags]
internal enum AuthenticatorDataFlags : byte
{
    None = 0,
    UserPresent = 0x01,
    UserVerified = 0x04,
    BackupEligible = 0x08,
    BackedUp = 0x10,
    AttestedCredentialData = 0x40,
    ExtensionData = 0x80
}
