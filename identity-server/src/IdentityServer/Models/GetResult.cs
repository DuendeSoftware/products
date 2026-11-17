// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Represents the result of a get (read) operation for an item of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the retrieved item.</typeparam>
public sealed record GetResult<T>
{
    /// <summary>
    /// Gets a value indicating whether the item was found.
    /// When <see langword="true"/>, <see cref="Item"/> and <see cref="Version"/> are not <see langword="null"/>.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Gets the retrieved item, or <see langword="null"/> if not found.
    /// </summary>
    public T? Item { get; init; }

    /// <summary>
    /// Gets the version of the retrieved item, or <see langword="null"/> if not found.
    /// </summary>
    public DataVersion? Version { get; init; }
}

/// <summary>
/// Factory methods for creating <see cref="GetResult{T}"/> instances.
/// </summary>
public static class GetResult
{
    /// <summary>Creates a found result.</summary>
    public static GetResult<T> Found<T>(T item, DataVersion version) => new()
    {
        Found = true,
        Item = item,
        Version = version
    };

    /// <summary>Creates a not-found result.</summary>
    public static GetResult<T> NotFound<T>() => new() { Found = false };
}
