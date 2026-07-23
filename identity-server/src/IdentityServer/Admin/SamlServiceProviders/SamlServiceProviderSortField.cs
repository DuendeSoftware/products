// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Fields available for sorting SAML Service Provider queries.
/// </summary>
public enum SamlServiceProviderSortField
{
    /// <summary>Sort by entity ID.</summary>
    EntityId,

    /// <summary>Sort by display name.</summary>
    DisplayName,

    /// <summary>Sort by enabled status.</summary>
    Enabled
}
