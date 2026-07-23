// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1000 // Do not declare static members on generic types - Factory methods are intended API design

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Represents the result of a get (read) operation for a DTO of type <typeparamref name="TDto"/>.
/// </summary>
/// <typeparam name="TDto">The type of the retrieved item.</typeparam>
public record GetResult<TDto>
{
    /// <summary>
    /// Gets a value indicating whether the item was found.
    /// When <see langword="true"/>, <see cref="Item"/> and <see cref="Version"/> are non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Item), nameof(Version))]
    public bool Found { get; internal set; }

    /// <summary>
    /// Gets the retrieved item, or <see langword="null"/> if not found.
    /// </summary>
    public TDto? Item { get; internal set; }

    /// <summary>
    /// Gets the version of the retrieved item, or <see langword="null"/> if not found.
    /// </summary>
    public DataVersion? Version { get; internal set; }
}

/// <summary>
/// Provides factory methods for creating <see cref="GetResult{TDto}"/> instances.
/// </summary>
public static class GetResult
{
    /// <summary>
    /// Creates a <see cref="GetResult{TDto}"/> indicating the item was found.
    /// </summary>
    /// <typeparam name="TDto">The type of the retrieved item.</typeparam>
    /// <param name="item">The retrieved item.</param>
    /// <param name="version">The version of the retrieved item.</param>
    public static GetResult<TDto> Found<TDto>(TDto item, DataVersion version) =>
        new()
        {
            Found = true,
            Item = item,
            Version = version
        };

    /// <summary>
    /// Creates a <see cref="GetResult{TDto}"/> indicating the item was not found.
    /// </summary>
    /// <typeparam name="TDto">The type of the item that was not found.</typeparam>
    public static GetResult<TDto> NotFound<TDto>() =>
        new()
        {
            Found = false
        };
}
