// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.ApiScopes;

/// <summary>
/// Represents an API scope configuration used to create a new API scope.
/// </summary>
public sealed class CreateApiScope
{
    public required string Name { get; set; }
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool ShowInDiscoveryDocument { get; set; } = true;
    public bool Required { get; set; }
    public bool Emphasize { get; set; }
    public List<string> UserClaims { get; set; } = [];
    public AttributeValueCollection ExtendedProperties { get; set; } = new();
}
