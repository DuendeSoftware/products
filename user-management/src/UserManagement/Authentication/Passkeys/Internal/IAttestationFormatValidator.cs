// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Validates attestation statements for a specific format.
/// Implement this interface to add support for custom attestation formats
/// during WebAuthn/Passkey registration.
/// </summary>
internal interface IAttestationFormatValidator
{
    /// <summary>
    /// The attestation format this validator handles (e.g., "none", "packed").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Validates the attestation statement.
    /// </summary>
    ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct);
}
