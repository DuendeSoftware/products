// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a Data Storage Object (DSO).
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface IDataStorageObject
{
    /// <summary>
    /// Gets the DSO version for this type.
    /// </summary>
    static abstract DataStorageObjectVersion DsoVersion { get; }
}
