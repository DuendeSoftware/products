// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Provides factory methods for creating <see cref="GetResult{T}"/> instances.
/// </summary>
public static class GetResult
{
    /// <summary>
    /// Creates a <see cref="GetResult{T}"/> indicating the item was found.
    /// </summary>
    /// <typeparam name="T">The type of the retrieved item.</typeparam>
    /// <param name="item">The retrieved item.</param>
    /// <param name="version">The version of the retrieved item.</param>
    public static GetResult<T> Found<T>(T item, DataVersion version) where T : notnull =>
        new()
        {
            Found = true,
            Item = item,
            Version = version
        };

    /// <summary>
    /// Creates a <see cref="GetResult{T}"/> indicating the item was not found.
    /// </summary>
    /// <typeparam name="T">The type of the item that was not found.</typeparam>
    public static GetResult<T> NotFound<T>() =>
        new()
        {
            Found = false
        };
}
