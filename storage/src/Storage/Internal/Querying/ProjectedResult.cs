// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Represents a query result with only selected field values rather than the full entity.
/// </summary>
public sealed record ProjectedResult
{
    /// <summary>
    /// The ID of the entity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The projected field values, keyed by field path.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Fields { get; }

    /// <summary>
    /// Creates a new projected result.
    /// </summary>
    /// <param name="id">The entity ID.</param>
    /// <param name="fields">The projected field values.</param>
    public ProjectedResult(Guid id, IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Id = id;
        Fields = fields;
    }
}
