// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Returns the current space, or <see cref="SpaceId.Default"/> when Spaces is not enabled or no space is set.
/// </summary>
internal delegate SpaceId GetSpaceIdOrDefault();
