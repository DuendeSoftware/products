// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.ApiResources;

/// <summary>
/// Represents an API resource configuration used to create a new API resource.
/// Initial secrets are not part of this model. To add secrets, use
/// <see cref="IApiResourceAdmin.CreateSecretAsync"/> after creation.
/// </summary>
public sealed class CreateApiResource
{
    public required string Name { get; set; }
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool ShowInDiscoveryDocument { get; set; } = true;
    public bool RequireResourceIndicator { get; set; }
    public List<string> UserClaims { get; set; } = [];
    public List<string> Scopes { get; set; } = [];
    public List<string> AllowedAccessTokenSigningAlgorithms { get; set; } = [];
    public AttributeValueCollection ExtendedProperties { get; set; } = new();
}
