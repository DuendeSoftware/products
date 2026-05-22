// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

/// <summary>
/// Represents an attribute path reference in a filter expression.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="path">The attribute path string.</param>
public sealed class AttributePathExpression(string path) : FilterExpression
{
    /// <summary>Gets the attribute path.</summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <inheritdoc />
    public override string ToString() => Path;
}
