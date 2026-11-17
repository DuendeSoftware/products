// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal static class ClientLinkDefinitions
{
    internal static readonly LinkDefinition ClientAllowedApiScope = new()
    {
        Left = ClientDso.EntityType,
        Right = ApiScopeDso.EntityType,
        Link = new LinkType(2115, "ClientAllowedApiScope"),
        OnRightDelete = LinkDeleteBehavior.Restrict
    };

    internal static readonly LinkDefinition ClientAllowedIdentityResource = new()
    {
        Left = ClientDso.EntityType,
        Right = IdentityResourceDso.EntityType,
        Link = new LinkType(2116, "ClientAllowedIdentityResource"),
        OnRightDelete = LinkDeleteBehavior.Restrict
    };
}
