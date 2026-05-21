// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a schema attribute code (programmatic identifier). Preserves the original
///     casing but compares case-insensitively (using ordinal ignore-case) for equality and hashing.
/// </summary>
[StringValue]
public partial record AttributeCode
{
    private const int MaxLength = 100;

    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    static string Normalize(string value) => value.Trim();

    private static bool TryValidate(string? input, out IReadOnlyList<string>? errors)
    {
        errors = null;

        if (input is null or { Length: 0 })
        {
            errors = ["A value is required."];
            return false;
        }

        var validationErrors = new List<string>();

        if (!char.IsAsciiLetter(input[0]))
        {
            validationErrors.Add("Must start with an ASCII letter.");
        }

        if (input[^1] == '_')
        {
            validationErrors.Add("Must not end with an underscore.");
        }

        foreach (var c in input.AsSpan())
        {
            if (!char.IsAsciiLetter(c) && !char.IsAsciiDigit(c) && c != '_')
            {
                validationErrors.Add("Must only contain ASCII letters, digits, or underscores.");
                break;
            }
        }

        if (validationErrors.Count > 0)
        {
            errors = validationErrors;
            return false;
        }

        return true;
    }
}
