// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.SamlServiceProviders;

internal static class SamlServiceProviderLinkDefinitions
{
    internal static readonly LinkDefinition SamlServiceProviderAllowedIdentityResource = new()
    {
        Left = SamlServiceProviderDso.EntityType,
        Right = IdentityResourceDso.EntityType,
        Link = new LinkType(2117, "SamlServiceProviderAllowedIdentityResource"),
        OnRightDelete = LinkDeleteBehavior.Restrict
    };
}
