// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Thrown inside a <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/>
/// factory callback to signal that the produced value must not be cached.
/// The caching stores catch this exception and return <c>null</c> to the caller.
/// </summary>
#pragma warning disable CA1032, CA1064 // Internal sentinel exception — not part of the public API
internal sealed class NotCachedException : Exception;
#pragma warning restore CA1032, CA1064
