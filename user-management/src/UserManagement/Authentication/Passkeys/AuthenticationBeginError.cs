// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Error codes for failures when beginning passkey authentication.
/// </summary>
public enum AuthenticationBeginError
{
    /// <summary>
    /// No user was found with the specified username.
    /// </summary>
    UserNotFound,

    /// <summary>
    /// The user does not have a passkey registered.
    /// </summary>
    NoPasskeyRegistered
}
