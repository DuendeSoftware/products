// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Options for creating a new public key credential (registration).
/// This corresponds to the WebAuthn PublicKeyCredentialCreationOptions dictionary.
/// </summary>
public sealed record PublicKeyCredentialCreationOptions
{
    /// <summary>
    /// Information about the relying party (RP).
    /// </summary>
    public required PublicKeyCredentialRelyingPartyEntity RelyingParty { get; init; }

    /// <summary>
    /// Information about the user account.
    /// </summary>
    public required PublicKeyCredentialUserEntity User { get; init; }

    /// <summary>
    /// A challenge intended to be used for generating the newly created credential's attestation object.
    /// Base64Url-encoded.
    /// </summary>
    public required string Challenge { get; init; }

    /// <summary>
    /// Information about the desired properties of the credential to be created.
    /// </summary>
    public required IReadOnlyList<PublicKeyCredentialParameters> PubKeyCredParams { get; init; }

    /// <summary>
    /// The Relying Party's preference regarding attestation conveyance.
    /// </summary>
    public string Attestation { get; init; } = PasskeyConstants.AttestationConveyance.None;

    /// <summary>
    /// Criteria for selecting an authenticator for credential creation.
    /// </summary>
    public AuthenticatorSelectionCriteria? AuthenticatorSelection { get; init; }

    /// <summary>
    /// List of credentials the Relying Party knows about for this user.
    /// Prevents re-registration of the same authenticator.
    /// </summary>
    public IReadOnlyList<PublicKeyCredentialDescriptor> ExcludeCredentials { get; init; } = [];

    /// <summary>
    /// A hint, in milliseconds, for how long the client should wait for the user
    /// to complete the registration ceremony before timing out.
    /// </summary>
    public uint? Timeout { get; init; }
}
