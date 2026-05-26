// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// The assertion response from the authenticator during authentication.
/// https://www.w3.org/TR/webauthn-3/#iface-authenticatorassertionresponse
/// </summary>
public sealed class AuthenticatorAssertionResponse
{
    /// <summary>
    /// The client data JSON (base64url-encoded).
    /// </summary>
    public required string ClientDataJSON { get; init; }

    /// <summary>
    /// The authenticator data (base64url-encoded).
    /// </summary>
    public required string AuthenticatorData { get; init; }

    /// <summary>
    /// The signature (base64url-encoded).
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// The user handle (base64url-encoded), if available.
    /// Used for usernameless authentication flows.
    /// </summary>
    public string? UserHandle { get; init; }
}
