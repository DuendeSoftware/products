// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.IdentityResources;

/// <summary>
/// Represents an identity resource configuration returned by admin read operations.
/// Immutable read model -- to modify, use <see cref="ToUpdate"/> to obtain a mutable
/// <see cref="UpdateIdentityResource"/> and pass it to <see cref="IIdentityResourceAdmin.UpdateAsync"/>.
/// </summary>
public sealed class IdentityResourceConfiguration
{
    /// <summary>
    /// The unique name of the identity resource. Required.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the identity resource is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// A display-friendly name for the identity resource.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// A description of the identity resource.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this resource is shown in the discovery document. Defaults to <see langword="true"/>.
    /// </summary>
    public bool ShowInDiscoveryDocument { get; init; } = true;

    /// <summary>
    /// Whether the user can de-select the scope on the consent screen. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Whether the consent screen will emphasize this scope. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Emphasize { get; init; }

    /// <summary>
    /// The collection of user claim types included when this resource is requested.
    /// </summary>
    public IReadOnlyList<string> UserClaims { get; init; } = [];

    /// <summary>
    /// Schema-validated extended properties for this identity resource.
    /// Values are validated against the registered identity resource schema when creating or updating.
    /// </summary>
    public IReadOnlyCollection<AttributeValue> ExtendedProperties { get; init; } = [];

    /// <summary>
    /// Creates an update model from this configuration.
    /// </summary>
    public UpdateIdentityResource ToUpdate() => new()
    {
        Name = Name,
        Enabled = Enabled,
        DisplayName = DisplayName,
        Description = Description,
        ShowInDiscoveryDocument = ShowInDiscoveryDocument,
        Required = Required,
        Emphasize = Emphasize,
        UserClaims = Copy(UserClaims),
        ExtendedProperties = CopyExtendedProperties()
    };

    /// <summary>
    /// Creates a create model from this configuration.
    /// </summary>
    public CreateIdentityResource ToCreate()
    {
        var update = ToUpdate();
        return new CreateIdentityResource
        {
            Name = update.Name,
            Enabled = update.Enabled,
            DisplayName = update.DisplayName,
            Description = update.Description,
            ShowInDiscoveryDocument = update.ShowInDiscoveryDocument,
            Required = update.Required,
            Emphasize = update.Emphasize,
            UserClaims = update.UserClaims,
            ExtendedProperties = update.ExtendedProperties
        };
    }

    private static List<string> Copy(IReadOnlyList<string> values) => [.. values];

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
