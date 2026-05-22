// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;

namespace Duende.Storage.Internal;

/// <summary>
/// The type of DSK that's being stored.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="Id">A number representation for the DSK type.</param>
/// <param name="Name">The name of the DSK type. This name is only used for display purposes and should never change.</param>
public readonly record struct DataStorageKeyType(uint Id, string Name)
{
    /// <summary>
    /// Obsolete parameterless constructor. Do not use.
    /// </summary>
    [Obsolete("Don't use this constructor")]
    public DataStorageKeyType() : this(0!, null!) => throw new InvalidOperationException("Cannot instantiate DSKType without parameters");

    /// <summary>
    /// Builds a <see cref="DataStorageKeyType"/> from an enum value.
    /// </summary>
    /// <param name="enum">The enum value to convert.</param>
    /// <returns>A <see cref="DataStorageKeyType"/> with the numeric and string representation of the enum.</returns>
    public static DataStorageKeyType BuildFrom(Enum @enum) =>
        new((uint)Convert.ToInt32(@enum, CultureInfo.InvariantCulture), @enum.ToString());

    /// <summary>
    /// Creates a <see cref="DataStorageKeyType"/> from an enum value.
    /// </summary>
    /// <param name="value">The enum value to convert.</param>
    /// <returns>A <see cref="DataStorageKeyType"/>.</returns>
    public static DataStorageKeyType FromEnum(Enum value) => BuildFrom(value);

    /// <summary>
    /// Implicitly converts an enum value to a <see cref="DataStorageKeyType"/>.
    /// </summary>
    /// <param name="value">The enum value to convert.</param>
    public static implicit operator DataStorageKeyType(Enum value) => BuildFrom(value);
}
