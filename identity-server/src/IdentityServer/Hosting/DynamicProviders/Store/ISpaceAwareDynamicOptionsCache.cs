// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Hosting.DynamicProviders.Store;

/// <summary>
/// Allows a dynamic scheme's cached options to be evicted for the current space.
/// </summary>
internal interface ISpaceAwareDynamicOptionsCache
{
    bool TryRemoveDynamic(string name);
}
