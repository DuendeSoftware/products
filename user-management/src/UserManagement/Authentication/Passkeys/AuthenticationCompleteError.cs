// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Error codes for failures when completing passkey authentication.
/// </summary>
public enum AuthenticationCompleteError
{
    /// <summary>
    /// The authentication challenge was not found.
    /// </summary>
    ChallengeNotFound,

    /// <summary>
    /// The client data JSON is invalid or malformed.
    /// </summary>
    InvalidClientData,

    /// <summary>
    /// The ceremony type is not 'webauthn.get'.
    /// </summary>
    InvalidType,

    /// <summary>
    /// The challenge does not match the stored challenge.
    /// </summary>
    ChallengeMismatch,

    /// <summary>
    /// The origin does not match the expected origin.
    /// </summary>
    OriginMismatch,

    /// <summary>
    /// The credential was not found.
    /// </summary>
    CredentialNotFound,

    /// <summary>
    /// The credential does not belong to the expected user.
    /// </summary>
    CredentialUserMismatch,

    /// <summary>
    /// The authenticator data is invalid or malformed.
    /// </summary>
    InvalidAuthenticatorData,

    /// <summary>
    /// The relying party ID hash does not match.
    /// </summary>
    RpIdMismatch,

    /// <summary>
    /// The user present flag is not set.
    /// </summary>
    UserNotPresent,

    /// <summary>
    /// User verification was required but not performed.
    /// </summary>
    UserVerificationRequired,

    /// <summary>
    /// The signature counter is invalid (possible credential cloning).
    /// </summary>
    InvalidSignCount,

    /// <summary>
    /// The signature verification failed.
    /// </summary>
    SignatureVerificationFailed,

    /// <summary>
    /// The signature is invalid or malformed (not valid Base64Url).
    /// </summary>
    InvalidSignature,

    /// <summary>
    /// The signing algorithm is not supported.
    /// </summary>
    UnsupportedAlgorithm,

    /// <summary>
    /// Failed to update the credential in storage.
    /// </summary>
    StorageError,

    /// <summary>
    /// The credential type is not "public-key".
    /// </summary>
    InvalidCredentialType,

    /// <summary>
    /// The authentication challenge has expired.
    /// </summary>
    ChallengeExpired,

    /// <summary>
    /// The backup eligibility (BE) flag changed since registration (WebAuthn 7.2.18-19).
    /// </summary>
    BackupEligibilityMismatch
}
