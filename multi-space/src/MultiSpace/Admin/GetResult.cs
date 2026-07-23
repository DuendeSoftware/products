// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1000

namespace Duende.MultiSpace;

/// <summary>
/// Represents the result of a get (read) operation for a DTO of type <typeparamref name="TDto"/>.
/// </summary>
/// <typeparam name="TDto">The type of the retrieved item.</typeparam>
public record GetResult<TDto>
{
    /// <summary>Gets a value indicating whether the item was found.</summary>
    [MemberNotNullWhen(true, nameof(Item), nameof(Version))]
    public bool Found { get; internal set; }

    /// <summary>Gets the retrieved item, or null if not found.</summary>
    public TDto? Item { get; internal set; }

    /// <summary>Gets the version of the retrieved item, or null if not found.</summary>
    public DataVersion? Version { get; internal set; }
}

/// <summary>
/// Provides factory methods for creating <see cref="GetResult{TDto}"/> instances.
/// </summary>
public static class GetResult
{
    /// <summary>Creates a found result.</summary>
    public static GetResult<TDto> Found<TDto>(TDto item, DataVersion version) =>
        new() { Found = true, Item = item, Version = version };

    /// <summary>Creates a not-found result.</summary>
    public static GetResult<TDto> NotFound<TDto>() =>
        new() { Found = false };
}
