// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.IdentityProviders;

/// <summary>
/// Represents an identity provider configuration used to update an existing identity provider.
/// </summary>
public sealed class UpdateIdentityProvider
{
    public required string Scheme { get; set; }
    public string? DisplayName { get; set; }
    public bool Enabled { get; set; } = true;
    public required string Type { get; set; }
    public AttributeValueCollection ExtendedProperties { get; set; } = new();
}
