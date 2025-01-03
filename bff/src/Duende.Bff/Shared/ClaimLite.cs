// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Serialization friendly claim
/// </summary>
internal class ClaimLite
{
    /// <summary>
    /// The type
    /// </summary>
    public string Type { get; init; } = default!;

    /// <summary>
    /// The value
    /// </summary>
    public string Value { get; init; } = default!;

    /// <summary>
    /// The value type
    /// </summary>
    public string? ValueType { get; init; }
}