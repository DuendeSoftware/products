// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Builder;

public sealed class DsoRegistration(Type dsoType, DataStorageObjectVersion dsoVersion)
{
    public Type DsoType { get; } = dsoType;
    public DataStorageObjectVersion DsoVersion { get; } = dsoVersion;
}
