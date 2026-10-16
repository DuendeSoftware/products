// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Validates attestation statements for a specific format.
/// Implement this interface to add support for custom attestation formats
/// during WebAuthn/Passkey registration.
/// </summary>
/// <remarks>
/// Implementations must use a fixed, non-empty format identifier and support resolution from the root
/// service provider. They are retained by the singleton authentication options validator, so scoped
/// dependencies are not supported. Constructors and format lookup must not depend directly or indirectly
/// on <see cref="UserAuthenticationOptions"/>, to avoid circular validation.
/// </remarks>
public interface IAttestationFormatValidator
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
