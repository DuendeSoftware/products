// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Runtime.CompilerServices;

namespace Duende.Storage.Internal.Querying.SearchFields;

/// <summary>
/// Immutable collection of search field values that can be passed to IStore.Create/Update methods.
/// Use <see cref="SearchFieldsBuilder"/> to construct instances.
/// </summary>
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

    public static SearchFieldCollection Create(ReadOnlySpan<SearchFieldValue> values) => new(values.ToArray());
}
