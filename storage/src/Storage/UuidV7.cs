// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

[ValueOf<Guid>]
public partial record UuidV7
{
    /// <summary>
    /// Creates a new UuidV7.
    /// </summary>
    public static UuidV7 New() => new(Guid.CreateVersion7());

    public static UuidV7 From(Guid value) => value;

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
