// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// Provides access to an ambient <see cref="PoolId"/> context, allowing downstream
/// services to resolve the correct pool without explicit parameters.
/// </summary>
internal interface IPoolContextAccessor
{
    /// <summary>
    /// Gets or sets the current pool context. When <c>null</c>, the default pool is used.
    /// </summary>
    PoolId? CurrentPoolId { get; set; }
}

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IPoolContextAccessor"/>.
/// </summary>
internal sealed class PoolContextAccessor : IPoolContextAccessor
{
    private readonly AsyncLocal<PoolId?> _current = new();

    public PoolId? CurrentPoolId
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
