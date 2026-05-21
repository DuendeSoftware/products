// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Specifies which attributes to include in query results.
/// When used, the query returns <see cref="EntityAttributeValue.AttributeValueCollection"/>
/// instead of a fully-typed DTO.
/// </summary>
public sealed record Projection
{
    /// <summary>The attribute names to include.</summary>
    public IReadOnlyList<string> Attributes { get; }

    /// <summary>
    /// Creates a projection for the specified attributes.
    /// </summary>
    public Projection(params string[] attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        if (attributes.Length == 0)
        {
            throw new ArgumentException("At least one attribute must be specified.", nameof(attributes));
        }

        foreach (var attr in attributes)
        {
            ArgumentException.ThrowIfNullOrEmpty(attr);
        }

        Attributes = attributes.ToArray();
    }

    /// <summary>
    /// Creates a projection for the specified attributes.
    /// </summary>
    public static Projection Of(params string[] attributes) => new(attributes);
}
