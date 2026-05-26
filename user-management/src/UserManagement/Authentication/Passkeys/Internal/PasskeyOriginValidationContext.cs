// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Context for passkey origin validation.
/// </summary>
internal sealed class PasskeyOriginValidationContext
{
    /// <summary>
    /// The fully-qualified origin from the credential's client data.
    /// Example: "https://app1.example.com"
    /// </summary>
    public required string Origin { get; init; }

    /// <summary>
    /// Whether the request came from a cross-origin iframe.
    /// </summary>
    public required bool CrossOrigin { get; init; }

    /// <summary>
    /// The configured allowlist of permitted origins.
    /// </summary>
    public required IReadOnlyList<string> AllowedOrigins { get; init; }
}
