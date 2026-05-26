// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// These attributes are used by the source generator to generate value objects.
// They must be replicated in each assembly that uses the source generator.
// In the 'root' namespace for that assembly.

namespace Duende.UserManagement;

/// <summary>
/// Marks a record as a value object. The source generator will create strongly-typed
/// value object members including parsing, validation, and serialization support.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class StringValue : Attribute
{
    /// <summary>
    /// Gets or sets whether to generate a ToString() method. Default is true.
    /// </summary>
    public bool GenerateToString { get; set; } = true;
}

/// <summary>
/// Marks a record as a value object. The source generator will create strongly-typed
/// value object members including parsing, validation, and serialization support.
/// </summary>
/// <typeparam name="T">The underlying value type.</typeparam>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class ValueOfAttribute<T> : Attribute
    where T : struct, IParsable<T>
{
    /// <summary>
    /// Gets or sets whether to generate a ToString() method. Default is true.
    /// </summary>
    public bool GenerateToString { get; set; } = true;
}

/// <summary>
/// Enum for the pre-defined character sets
/// </summary>
[Flags]
internal enum CharSet
{
    None = 0,
    LowercaseLatin = 1, // a-z
    UppercaseLatin = 2, // A-Z
    Digits = 4,         // 0-9
    Symbols = 8,        // e.g., !@#$%^&*()

    // Combinations
    LatinLetters = LowercaseLatin | UppercaseLatin,
    AlphaNumeric = LatinLetters | Digits
}
