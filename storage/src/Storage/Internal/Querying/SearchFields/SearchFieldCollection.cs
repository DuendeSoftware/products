// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Runtime.CompilerServices;

namespace Duende.Storage.Internal.Querying.SearchFields;

/// <summary>
/// Immutable collection of search field values that can be passed to IStore.Create/Update methods.
/// Use <see cref="SearchFieldsBuilder"/> to construct instances.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
[CollectionBuilder(typeof(SearchFieldCollection), nameof(Create))]
public sealed class SearchFieldCollection(IReadOnlyList<SearchFieldValue> values) : IReadOnlyCollection<SearchFieldValue>
{
    /// <summary>
    /// Gets the number of search field values in this collection.
    /// </summary>
    public int Count => values.Count;

    /// <summary>
    /// Returns an enumerator that iterates through the search field values.
    /// </summary>
    public IEnumerator<SearchFieldValue> GetEnumerator() => values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Creates an empty SearchFields collection.
    /// </summary>
    public static SearchFieldCollection Empty { get; } = new(Array.Empty<SearchFieldValue>());

    /// <summary>
    /// Creates a <see cref="SearchFieldCollection"/> from a span of values.
    /// </summary>
    /// <param name="values">The values to include.</param>
    /// <returns>A new <see cref="SearchFieldCollection"/>.</returns>
    public static SearchFieldCollection Create(ReadOnlySpan<SearchFieldValue> values) => new(values.ToArray());
}
