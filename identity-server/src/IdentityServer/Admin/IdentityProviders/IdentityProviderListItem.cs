// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.IdentityProviders;

/// <summary>
/// A lightweight projection of an identity provider for use in list/query results.
/// </summary>
public sealed record IdentityProviderListItem
{
    /// <summary>
    /// The storage identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The authentication scheme name.
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// The display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Whether the provider is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// The protocol type (e.g. <c>"oidc"</c>).
    /// </summary>
    public required string Type { get; init; }
}
