// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Specifies requirements for the authenticator during registration.
/// This corresponds to the WebAuthn AuthenticatorSelectionCriteria dictionary.
/// See https://www.w3.org/TR/webauthn-3/#dictdef-authenticatorselectioncriteria
/// </summary>
public sealed record AuthenticatorSelectionCriteria
{
    /// <summary>
    /// Restricts the authenticator attachment modality.
    /// Possible values: "platform", "cross-platform", or null.
    /// </summary>
    public string? AuthenticatorAttachment { get; init; }

    /// <summary>
    /// User verification requirement for credential creation.
    /// Possible values: "required", "preferred", "discouraged", or null.
    /// </summary>
    public string? UserVerification { get; init; }

    /// <summary>
    /// Resident key requirement for credential creation.
    /// Possible values: "discouraged", "preferred", "required", or null.
    /// See https://www.w3.org/TR/webauthn-3/#enum-residentKeyRequirement
    /// </summary>
    public string? ResidentKey { get; init; }
}
