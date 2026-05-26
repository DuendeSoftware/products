// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Error codes for passkey registration failures.
/// </summary>
public enum RegistrationError
{
    /// <summary>
    /// The registration session was not found.
    /// </summary>
    ChallengeNotFound,

    /// <summary>
    /// The client data JSON is malformed or missing required fields.
    /// </summary>
    InvalidClientData,

    /// <summary>
    /// The challenge in the response doesn't match the stored challenge.
    /// </summary>
    ChallengeMismatch,

    /// <summary>
    /// The origin in the response doesn't match the expected origin.
    /// </summary>
    OriginMismatch,

    /// <summary>
    /// The type field is not "webauthn.create".
    /// </summary>
    InvalidType,

    /// <summary>
    /// The attestation object is malformed CBOR.
    /// </summary>
    InvalidAttestationObject,

    /// <summary>
    /// The attestation format is not supported.
    /// </summary>
    UnsupportedAttestationFormat,

    /// <summary>
    /// The attestation statement validation failed.
    /// </summary>
    AttestationValidationFailed,

    /// <summary>
    /// The COSE algorithm is not supported.
    /// </summary>
    UnsupportedAlgorithm,

    /// <summary>
    /// The RP ID hash doesn't match the expected value.
    /// </summary>
    RpIdMismatch,

    /// <summary>
    /// The user presence flag is not set.
    /// </summary>
    UserNotPresent,

    /// <summary>
    /// The credential type is not "public-key".
    /// </summary>
    InvalidCredentialType,

    /// <summary>
    /// User verification was required but not performed.
    /// </summary>
    UserVerificationRequired,

    /// <summary>
    /// The registration challenge has expired.
    /// </summary>
    ChallengeExpired,

    /// <summary>
    /// An attestation trust policy rejected the authenticator.
    /// </summary>
    AttestationTrustPolicyFailed,

    /// <summary>
    /// The backup state (BS) flag is set without backup eligibility (BE) (WebAuthn 7.1.18).
    /// </summary>
    InvalidBackupState
}
