// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Marks a dsk as only being a guid value. This means it won't be stored as serialized json
/// but only uses the guid value. IE: UserSubjectId, which is already a Guid. 
/// </summary>
public interface IGuidDataStorageKey : IDataStorageKey
{
    Guid Value { get; }
}
