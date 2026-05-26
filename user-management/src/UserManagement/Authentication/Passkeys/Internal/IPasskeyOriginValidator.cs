// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Validates the origin of a passkey request.
/// </summary>
internal interface IPasskeyOriginValidator
{
    /// <summary>
    /// Validates whether the origin is allowed for passkey operations.
    /// </summary>
    /// <param name="context">The validation context containing origin information.</param>
    /// <returns>True if the origin is valid, false otherwise.</returns>
    ValueTask<bool> ValidateAsync(PasskeyOriginValidationContext context);
}
