// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Wraps a typed DSO value with its entity type and version metadata.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record TypedDso
{
    /// <summary>
    /// Creates a <see cref="TypedDso"/> from a typed DSO value.
    /// </summary>
    /// <typeparam name="TDso">The DSO type.</typeparam>
    /// <param name="value">The DSO value.</param>
    /// <returns>A <see cref="TypedDso"/> wrapping the value with its metadata.</returns>
    public static TypedDso For<TDso>(TDso value) where TDso : IDataStorageObject => new TypedDso
    {
        Value = value,
        EntityType = TDso.DsoVersion.EntityType,
        Version = TDso.DsoVersion
    };

    /// <summary>
    /// Gets the DSO value.
    /// </summary>
    public required IDataStorageObject Value { get; init; }

    /// <summary>
    /// Gets the entity type.
    /// </summary>
    public required EntityType EntityType { get; init; }

    /// <summary>
    /// Gets the DSO version.
    /// </summary>
    public required DataStorageObjectVersion Version { get; init; }
}
