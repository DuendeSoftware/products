// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// A value object wrapping a version 7 UUID (time-ordered GUID).
/// </summary>
[ValueOf<Guid>]
public partial record UuidV7
{
    /// <summary>
    /// Creates a new UuidV7.
    /// </summary>
    public static UuidV7 New() => new(Guid.CreateVersion7());

    /// <summary>
    /// Creates a <see cref="UuidV7"/> from the specified <see cref="Guid"/> value.
    /// </summary>
    /// <param name="value">The GUID value to wrap.</param>
    /// <returns>A new <see cref="UuidV7"/> instance.</returns>
    public static UuidV7 From(Guid value) => value;

    /// <summary>
    /// Validates that the specified GUID is a valid version 7 UUID.
    /// </summary>
    /// <param name="input">The GUID to validate.</param>
    /// <param name="errors">When validation fails, contains the list of validation errors.</param>
    /// <returns><c>true</c> if the input is a valid UUIDv7; otherwise, <c>false</c>.</returns>
    public static bool TryValidate(Guid? input, out IReadOnlyList<string>? errors)
    {
        errors = null;

        if (input == null)
        {
            errors = ["No UUID value provided"];
            return false;
        }

        var value = input.Value;
        if (value.Variant is < 0x8 or > 0xB)
        {
            errors = [$"Not a valid UUIDV7 Guid: Invalid variant: {value.Variant:X}"];
            return false;
        }

        if (value.Version != 7)
        {
            errors = [$"Not a valid UUIDV7 Guid: Version is {value.Version} but should be 7."];
            return false;
        }

        return true;
    }
}
