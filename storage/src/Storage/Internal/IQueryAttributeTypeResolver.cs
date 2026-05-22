// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal;

/// <summary>
/// Resolves query attribute paths to Faro Field types.
/// Implementations define the schema-specific mapping for a resource type (User, Group, etc.).
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface IQueryAttributeTypeResolver
{
    /// <summary>
    /// Resolves a query attribute path to its corresponding Faro Field.
    /// </summary>
    /// <param name="attributePath">The attribute path (e.g., "userName", "name.familyName", "emails.value").</param>
    /// <returns>The corresponding Faro Field instance.</returns>
    /// <exception cref="NotSupportedException">Thrown when the attribute path is not recognized.</exception>
    Field ResolveField(string attributePath);
}
