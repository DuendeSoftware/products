// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;

namespace Duende.Storage.Internal;

/// <summary>
/// The type of DSK that's being stored. 
/// </summary>
/// <param name="Id">A number representation for the DSK type.</param>
/// <param name="Name">The name of the DSK type. This name is only used for display purposes and should never change.  </param>
public readonly record struct DataStorageKeyType(uint Id, string Name)
{
    [Obsolete("Don't use this constructor")]
    public DataStorageKeyType() : this(0!, null!) => throw new InvalidOperationException("Cannot instantiate DSKType without parameters");

    public static DataStorageKeyType BuildFrom(Enum @enum) =>
        new((uint)Convert.ToInt32(@enum, CultureInfo.InvariantCulture), @enum.ToString());

    public static DataStorageKeyType FromEnum(Enum value) => BuildFrom(value);

    public static implicit operator DataStorageKeyType(Enum value) => BuildFrom(value);
}
