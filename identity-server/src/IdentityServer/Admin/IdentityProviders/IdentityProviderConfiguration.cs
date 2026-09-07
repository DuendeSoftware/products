// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.IdentityProviders;

/// <summary>
/// Represents an identity provider configuration returned by admin read operations.
/// Immutable read model -- to modify, use <see cref="ToUpdate"/> to obtain a mutable
/// <see cref="UpdateIdentityProvider"/> and pass it to <see cref="IIdentityProviderAdmin.UpdateAsync"/>.
/// </summary>
public sealed class IdentityProviderConfiguration
{
    /// <summary>
    /// The authentication scheme name. Required. Primary business identifier.
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// A display-friendly name for the provider (used in login UI elements such as external login buttons).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Whether the provider is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The protocol type of the provider (e.g. <c>"oidc"</c> for OpenID Connect). Required.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Protocol-specific and extended configuration properties validated against the per-type schema
    /// (e.g. <c>idp:oidc</c> for OIDC providers).
    /// For OIDC providers this includes Authority, ClientId, ClientSecret, ResponseType, Scope, etc.
    /// </summary>
    public IReadOnlyCollection<AttributeValue> ExtendedProperties { get; init; } = [];

    /// <summary>
    /// Creates an update model from this configuration.
    /// </summary>
    public UpdateIdentityProvider ToUpdate() => new()
    {
        Scheme = Scheme,
        DisplayName = DisplayName,
        Enabled = Enabled,
        Type = Type,
        ExtendedProperties = CopyExtendedProperties()
    };

    /// <summary>
    /// Creates a create model from this configuration.
    /// </summary>
    public CreateIdentityProvider ToCreate()
    {
        var update = ToUpdate();
        return new CreateIdentityProvider
        {
            Scheme = update.Scheme,
            DisplayName = update.DisplayName,
            Enabled = update.Enabled,
            Type = update.Type,
            ExtendedProperties = update.ExtendedProperties
        };
    }

    private AttributeValueCollection CopyExtendedProperties()
    {
        var copy = new AttributeValueCollection();
        foreach (var attribute in ExtendedProperties)
        {
            copy.Set(attribute);
        }

        return copy;
    }
}
