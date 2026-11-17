// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

internal static class ApiResourceLinkDefinitions
{
    internal static readonly LinkDefinition ApiResourceScope = new()
    {
        Left = ApiResourceDso.EntityType,
        Right = ApiScopeDso.EntityType,
        Link = new LinkType(2100, "ApiResourceScope"),
        OnRightDelete = LinkDeleteBehavior.Restrict
    };
}
