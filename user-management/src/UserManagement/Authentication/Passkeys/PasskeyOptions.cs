// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Configuration options for passkey registration and authentication.
/// </summary>
public sealed class PasskeyOptions
{

    /// <summary>
    /// The human-readable name of the relying party.
    /// </summary>
    /// <example>"My Application" or "ACME Corporation"</example>
    /// <remarks>
    /// This property is for display purposes only and does not affect security.
    /// Defaults to the name of the assembly containing <see cref="PasskeyOptions"/> if not explicitly set.
    /// </remarks>
    public string RelyingPartyName { get; set; } = typeof(PasskeyOptions).Assembly.GetName().Name!;

    /// <summary>
    /// Size of the challenge in bytes. Default is 32 bytes (256 bits).
    /// </summary>
    public int ChallengeSize { get; set; } = 32;

    /// <summary>
    /// User verification requirement for passkey authentication.
    ///
    /// <list type="bullet">
    ///   <item><description>"required" (<see cref="PasskeyConstants.UserVerificationRequirement.Required"/>) - User verification must be performed (e.g., PIN, biometric)</description></item>
    ///   <item><description>"preferred" (<see cref="PasskeyConstants.UserVerificationRequirement.Preferred"/>) - User verification is preferred but not required (default)</description></item>
    ///   <item><description>"discouraged" (<see cref="PasskeyConstants.UserVerificationRequirement.Discouraged"/>) - User verification should not be performed</description></item>
    /// </list>
    /// </summary>
    public string UserVerificationRequirement { get; set; } = PasskeyConstants.UserVerificationRequirement.Preferred;

    /// <summary>
    /// Attestation conveyance preference for credential creation.
    ///
    /// <list type="bullet">
    ///   <item><description>"none" (<see cref="PasskeyConstants.AttestationConveyance.None"/>) - No attestation statement is needed</description></item>
    ///   <item><description>"indirect" (<see cref="PasskeyConstants.AttestationConveyance.Indirect"/>) - Attestation statement may be anonymized</description></item>
    ///   <item><description>"direct" (<see cref="PasskeyConstants.AttestationConveyance.Direct"/>) - Attestation statement should be provided directly</description></item>
    ///   <item><description>"enterprise" (<see cref="PasskeyConstants.AttestationConveyance.Enterprise"/>) - Enterprise attestation for managed authenticators</description></item>
    /// </list>
    /// </summary>
    public string AttestationConveyancePreference { get; set; } = PasskeyConstants.AttestationConveyance.None;

    /// <summary>
    /// Restricts the authenticator attachment modality.
    ///
    /// <list type="bullet">
    ///   <item><description>"platform" (<see cref="PasskeyConstants.AuthenticatorAttachment.Platform"/>) - Built-in authenticators (Windows Hello, Touch ID, Face ID)</description></item>
    ///   <item><description>"cross-platform" (<see cref="PasskeyConstants.AuthenticatorAttachment.CrossPlatform"/>) - Roaming authenticators (USB security keys, Bluetooth)</description></item>
    ///   <item><description><c>null</c> - Any authenticator type allowed (default)</description></item>
    /// </list>
    ///
    /// See <see href="https://www.w3.org/TR/webauthn-3/#enumdef-authenticatorattachment"/>.
    /// </summary>
    public string? AuthenticatorAttachment { get; set; }

    /// <summary>
    /// Resident key (discoverable credential) requirement for passkey registration.
    ///
    /// <list type="bullet">
    ///   <item><description>"discouraged" (<see cref="PasskeyConstants.ResidentKeyRequirement.Discouraged"/>) - RP prefers non-discoverable credential</description></item>
    ///   <item><description>"preferred" (<see cref="PasskeyConstants.ResidentKeyRequirement.Preferred"/>) - RP prefers discoverable if authenticator supports it (default)</description></item>
    ///   <item><description>"required" (<see cref="PasskeyConstants.ResidentKeyRequirement.Required"/>) - RP requires discoverable credential</description></item>
    /// </list>
    ///
    /// See <see href="https://www.w3.org/TR/webauthn-3/#enum-residentKeyRequirement"/>.
    /// </summary>
    public string ResidentKeyRequirement { get; set; } = PasskeyConstants.ResidentKeyRequirement.Preferred;

    /// <summary>
    /// The effective domain of the server, used as the Relying Party ID.
    ///
    /// See <see href="https://www.w3.org/TR/webauthn-3/#rp-id"/>.
    /// </summary>
    /// <remarks>
    /// This value is used as the WebAuthn Relying Party ID for both browser options and
    /// server-side verification.
    /// Set this explicitly when you need a single RP ID for multiple hostnames,
    /// or to share passkeys across subdomains
    /// (e.g., <c>"example.com"</c> for <c>auth.example.com</c> and <c>app.example.com</c>).
    /// </remarks>
    /// <example>"example.com"</example>
    public string? ServerDomain { get; set; }

    /// <summary>
    /// Optional list of COSE algorithm identifiers to support, in preference order.
    /// Use <see cref="CoseAlgorithms"/> to specify algorithm identifiers.
    ///
    /// See <see href="https://www.iana.org/assignments/cose/cose.xhtml#algorithms"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// // Prefer RS256 over ES256
    /// options.SupportedAlgorithms = [CoseAlgorithms.Rs256, CoseAlgorithms.Es256];
    ///
    /// // ES256 only
    /// options.SupportedAlgorithms = [CoseAlgorithms.Es256];
    /// </code>
    /// </example>
    public IReadOnlyList<int> SupportedAlgorithms { get; set; } = [];

    /// <summary>
    /// The maximum time a passkey challenge is valid before it expires.
    /// Challenges are single-use and will be rejected after this timeout.
    /// Default is 5 minutes (300 seconds).
    /// </summary>
    public TimeSpan ChallengeTimeout { get; set; } = TimeSpan.FromSeconds(300);

    /// <summary>
    /// A list of fully-qualified origins (scheme + host + optional port) that are
    /// permitted to use passkeys with this relying party.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each entry must be a fully-qualified origin such as <c>"https://app.example.com"</c> or
    /// <c>"https://example.com:8443"</c>. Entries must contain only scheme, host, and optional
    /// port. Path, query string, fragment, and user-info components are ignored and do not produce
    /// a match.
    /// </para>
    /// <para>
    /// This list must contain at least one allowed origin. The <c>clientDataJSON.origin</c>
    /// received from the authenticator is validated against every configured entry using exact
    /// scheme, host, and port comparison. If no valid entry matches, registration or
    /// authentication is rejected.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// options.AllowedOrigins = ["https://app.example.com", "https://auth.example.com"];
    /// </code>
    /// </example>
    public IReadOnlyList<string>? AllowedOrigins { get; set; }
}
