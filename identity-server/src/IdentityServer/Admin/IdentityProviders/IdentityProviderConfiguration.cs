// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.IdentityProviders;

/// <summary>
/// Represents an identity provider configuration for admin CRUD operations.
/// Mutable class — callers can <c>Get</c>, modify properties, and pass back to <c>UpdateAsync</c>.
/// </summary>
public class IdentityProviderConfiguration
{
    /// <summary>
    /// The authentication scheme name. Required. Primary business identifier.
    /// </summary>
    public required string Scheme { get; set; }

    /// <summary>
    /// A display-friendly name for the provider (used in login UI elements such as external login buttons).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether the provider is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The protocol type of the provider (e.g. <c>"oidc"</c> for OpenID Connect). Required.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Protocol-specific configuration properties stored as a key-value dictionary.
    /// For OIDC providers this includes Authority, ClientId, ClientSecret, ResponseType, Scope, etc.
    /// </summary>
    public Dictionary<string, string>? Properties { get; set; }

    /// <summary>
    /// Data version for optimistic concurrency. <see langword="null"/> for new providers.
    /// </summary>
    public DataVersion? Version { get; init; }
}
