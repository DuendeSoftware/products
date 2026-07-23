// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a pool identifier.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
[ValueOf<int>]
public partial record PoolId
{
    /// <summary>The default pool identifier (pool 0), used for single-space deployments.</summary>
    public static readonly PoolId Default = 0;
}
