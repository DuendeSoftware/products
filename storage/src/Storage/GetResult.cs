// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage;

/// <summary>
/// Represents the result of a get (read) operation for an item of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the retrieved item.</typeparam>
public record GetResult<T>
{
    internal GetResult()
    {
    }

    /// <summary>
    /// Gets a value indicating whether the item was found.
    /// When <see langword="true"/>, <see cref="Item"/> and <see cref="Version"/> are not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Item), nameof(Version))]
    public bool Found { get; internal set; }

    /// <summary>
    /// Gets the retrieved item, or <see langword="null"/> if not found.
    /// </summary>
    public T? Item { get; internal set; }

    /// <summary>
    /// Gets the version of the retrieved item, or <see langword="null"/> if not found.
    /// </summary>
    public DataVersion? Version { get; internal set; }
}
