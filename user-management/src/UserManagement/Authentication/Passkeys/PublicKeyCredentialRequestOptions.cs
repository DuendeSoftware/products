// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Options for navigator.credentials.get().
/// https://www.w3.org/TR/webauthn-3/#dictdef-publickeycredentialrequestoptions
/// </summary>
public sealed record PublicKeyCredentialRequestOptions
{
    /// <summary>
    /// The cryptographic challenge (base64url-encoded).
    /// </summary>
    public required string Challenge { get; init; }

    /// <summary>
    /// The relying party ID. Defaults to the origin's effective domain.
    /// </summary>
    public string? RpId { get; init; }

    /// <summary>
    /// The time in milliseconds that the caller is willing to wait for the call to complete.
    /// </summary>
    public uint? Timeout { get; init; }

    /// <summary>
    /// The list of credentials the caller is willing to accept.
    /// </summary>
    public required IReadOnlyList<PublicKeyCredentialDescriptor> AllowCredentials { get; init; }

    /// <summary>
    /// The user verification requirement.
    /// </summary>
    public string UserVerification { get; init; } = PasskeyConstants.UserVerificationRequirement.Preferred;
}
