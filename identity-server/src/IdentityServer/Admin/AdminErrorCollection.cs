// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections;
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Represents a collection of admin operation errors.
/// </summary>
public sealed class AdminErrorCollection : IReadOnlyList<AdminError>
{
    private readonly ReadOnlyCollection<AdminError> _errors;

    /// <summary>
    /// Creates a new error collection.
    /// </summary>
    /// <param name="errors">The errors in the collection.</param>
    public AdminErrorCollection(IEnumerable<AdminError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _errors = errors.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public int Count => _errors.Count;

    /// <inheritdoc />
    public AdminError this[int index] => _errors[index];

    /// <inheritdoc />
    public IEnumerator<AdminError> GetEnumerator() => _errors.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() => string.Join(", ", _errors.Select(x => x.ToString()));
}
