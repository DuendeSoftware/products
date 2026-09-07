// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.ApiResources;

/// <summary>
/// Represents an API resource configuration returned by admin read operations.
/// Immutable read model -- to modify, use <see cref="ToUpdate"/> to obtain a mutable
/// <see cref="UpdateApiResource"/> and pass it to <see cref="IApiResourceAdmin.UpdateAsync"/>.
/// </summary>
public sealed class ApiResourceConfiguration
{
    /// <summary>
    /// The unique name of the API resource. Required.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the API resource is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// A display-friendly name for the API resource.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// A description of the API resource.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this resource is shown in the discovery document. Defaults to <see langword="true"/>.
    /// </summary>
    public bool ShowInDiscoveryDocument { get; init; } = true;

    /// <summary>
    /// Whether this API resource requires the resource indicator to request it.
    /// </summary>
    public bool RequireResourceIndicator { get; init; }

    /// <summary>
    /// The collection of user claim types included when this resource is requested.
    /// </summary>
    public IReadOnlyList<string> UserClaims { get; init; } = [];

    /// <summary>
    /// The collection of API scope names that this API resource exposes.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// The collection of allowed signing algorithms for access tokens issued to this resource.
    /// </summary>
    public IReadOnlyList<string> AllowedAccessTokenSigningAlgorithms { get; init; } = [];

    /// <summary>
    /// API secrets: metadata only. The secret value is never exposed.
    /// To add a new secret, use <see cref="IApiResourceAdmin.CreateSecretAsync"/>.
    /// To change or remove a secret, delete it and create a new one.
    /// </summary>
    public IReadOnlyList<ApiResourceSecretConfiguration> ApiSecrets { get; init; } = [];

    /// <summary>
    /// Schema-validated extended properties for this API resource.
    /// Values are validated against the registered API resource schema when creating or updating.
    /// </summary>
    public IReadOnlyCollection<AttributeValue> ExtendedProperties { get; init; } = [];

    /// <summary>
    /// Creates an update model from this configuration.
    /// </summary>
    public UpdateApiResource ToUpdate() => new()
    {
        Name = Name,
        Enabled = Enabled,
        DisplayName = DisplayName,
        Description = Description,
        ShowInDiscoveryDocument = ShowInDiscoveryDocument,
        RequireResourceIndicator = RequireResourceIndicator,
        UserClaims = Copy(UserClaims),
        Scopes = Copy(Scopes),
        AllowedAccessTokenSigningAlgorithms = Copy(AllowedAccessTokenSigningAlgorithms),
        ExtendedProperties = CopyExtendedProperties()
    };

    /// <summary>
    /// Creates a create model from this configuration.
    /// </summary>
    public CreateApiResource ToCreate()
    {
        var update = ToUpdate();
        return new CreateApiResource
        {
            Name = update.Name,
            Enabled = update.Enabled,
            DisplayName = update.DisplayName,
            Description = update.Description,
            ShowInDiscoveryDocument = update.ShowInDiscoveryDocument,
            RequireResourceIndicator = update.RequireResourceIndicator,
            UserClaims = update.UserClaims,
            Scopes = update.Scopes,
            AllowedAccessTokenSigningAlgorithms = update.AllowedAccessTokenSigningAlgorithms,
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
