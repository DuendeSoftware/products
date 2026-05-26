// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Constants for WebAuthn/Passkey operations as defined in the W3C WebAuthn specification.
/// </summary>
/// <remarks>
/// See: https://www.w3.org/TR/webauthn-3/
/// </remarks>
public static class PasskeyConstants
{
    /// <summary>
    /// Authenticator attachment modality values.
    /// See: https://www.w3.org/TR/webauthn-3/#enumdef-authenticatorattachment
    /// </summary>
    public static class AuthenticatorAttachment
    {
        /// <summary>
        /// Platform authenticator - built-in to the device (Windows Hello, Touch ID, Face ID).
        /// </summary>
        public const string Platform = "platform";

        /// <summary>
        /// Cross-platform authenticator - roaming authenticators (USB security keys, Bluetooth).
        /// </summary>
        public const string CrossPlatform = "cross-platform";
    }

    /// <summary>
    /// User verification requirement values.
    /// See: https://www.w3.org/TR/webauthn-3/#enumdef-userverificationrequirement
    /// </summary>
    public static class UserVerificationRequirement
    {
        /// <summary>
        /// User verification is required (e.g., PIN, biometric).
        /// </summary>
        public const string Required = "required";

        /// <summary>
        /// User verification is preferred but not required.
        /// </summary>
        public const string Preferred = "preferred";

        /// <summary>
        /// User verification should not be performed.
        /// </summary>
        public const string Discouraged = "discouraged";
    }

    /// <summary>
    /// Resident key (discoverable credential) requirement values.
    /// See: https://www.w3.org/TR/webauthn-3/#enum-residentKeyRequirement
    /// </summary>
    public static class ResidentKeyRequirement
    {
        /// <summary>
        /// Relying party requires a discoverable credential.
        /// </summary>
        public const string Required = "required";

        /// <summary>
        /// Relying party prefers a discoverable credential if the authenticator supports it.
        /// </summary>
        public const string Preferred = "preferred";

        /// <summary>
        /// Relying party prefers a non-discoverable credential.
        /// </summary>
        public const string Discouraged = "discouraged";
    }

    /// <summary>
    /// Attestation conveyance preference values.
    /// See: https://www.w3.org/TR/webauthn-3/#enumdef-attestationconveyancepreference
    /// </summary>
    public static class AttestationConveyance
    {
        /// <summary>
        /// No attestation statement is needed.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Attestation statement may be anonymized.
        /// </summary>
        public const string Indirect = "indirect";

        /// <summary>
        /// Attestation statement should be provided directly.
        /// </summary>
        public const string Direct = "direct";

        /// <summary>
        /// Attestation statement should be provided directly.
        /// </summary>
        public const string Enterprise = "enterprise";
    }

    /// <summary>
    /// Credential type values.
    /// See: https://www.w3.org/TR/webauthn-3/#enumdef-publickeycredentialtype
    /// </summary>
    public static class CredentialType
    {
        /// <summary>
        /// Public key credential type.
        /// </summary>
        public const string PublicKey = "public-key";
    }

    /// <summary>
    /// Attestation statement format identifiers.
    /// See: https://www.w3.org/TR/webauthn-3/#sctn-defined-attestation-formats
    /// </summary>
    public static class AttestationFormat
    {
        /// <summary>
        /// No attestation statement is provided.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Packed attestation statement format.
        /// </summary>
        public const string Packed = "packed";

        /// <summary>
        /// TPM attestation statement format.
        /// </summary>
        public const string Tpm = "tpm";
    }

    /// <summary>
    /// Client data type values for WebAuthn ceremonies.
    /// </summary>
    public static class ClientDataType
    {
        /// <summary>
        /// Client data type for credential creation (registration).
        /// </summary>
        public const string Create = "webauthn.create";

        /// <summary>
        /// Client data type for credential assertion (authentication).
        /// </summary>
        public const string Get = "webauthn.get";
    }
}
