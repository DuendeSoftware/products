// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Class to model entries in CORS origin cache.
/// </summary>
public class CorsCacheEntry
{
    /// <summary>
    /// Ctor.
    /// </summary>
    [JsonConstructor]
    public CorsCacheEntry(bool allowed) => Allowed = allowed;

    /// <summary>
    /// Is origin allowed.
    /// </summary>
    public bool Allowed { get; }
}
