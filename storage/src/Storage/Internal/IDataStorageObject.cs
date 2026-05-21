// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a Data Storage Object (DSO).
/// </summary>
public interface IDataStorageObject
{
    static abstract DataStorageObjectVersion DsoVersion { get; }
}
