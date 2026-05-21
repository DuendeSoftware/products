// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Interface for DSK (Data Store Key) types.
/// </summary>
public interface IDataStorageKey
{
    /// <summary>
    /// The version of the DSK. DSK's (once released) must be immutable, so we need
    /// to keep track of versions. 
    /// </summary>
    static abstract DataStorageKeyVersion DskVersion { get; }
}
