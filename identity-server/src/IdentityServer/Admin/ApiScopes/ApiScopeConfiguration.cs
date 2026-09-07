// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.ApiScopes;

/// <summary>
/// Represents an API scope configuration returned by admin read operations.
/// Immutable read model -- to modify, use <see cref="ToUpdate"/> to obtain a mutable
/// <see cref="UpdateApiScope"/> and pass it to <see cref="IApiScopeAdmin.UpdateAsync"/>.
/// </summary>
public sealed class ApiScopeConfiguration
{
    /// <summary>
    /// The unique name of the API scope. Required.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the API scope is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// A display-friendly name for the API scope.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// A description of the API scope.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this scope is shown in the discovery document. Defaults to <see langword="true"/>.
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
    /// The collection of user claim types included when this scope is requested.
    /// </summary>
    public IReadOnlyList<string> UserClaims { get; init; } = [];

    /// <summary>
    /// Schema-validated extended properties for this API scope.
    /// Values are validated against the registered API scope schema when creating or updating.
    /// </summary>
    public IReadOnlyCollection<AttributeValue> ExtendedProperties { get; init; } = [];

    /// <summary>
    /// Creates an update model from this configuration.
    /// </summary>
    public UpdateApiScope ToUpdate() => new()
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
    public CreateApiScope ToCreate()
    {
        var update = ToUpdate();
        return new CreateApiScope
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
