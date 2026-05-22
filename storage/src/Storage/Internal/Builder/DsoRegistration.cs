// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Builder;

/// <summary>
/// Represents a DSO type registration binding a CLR type to its DSO version.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal sealed class DsoRegistration(Type dsoType, DataStorageObjectVersion dsoVersion)
{
    /// <summary>
    /// Gets the CLR type of the DSO.
    /// </summary>
    public Type DsoType { get; } = dsoType;

    /// <summary>
    /// Gets the DSO version.
    /// </summary>
    public DataStorageObjectVersion DsoVersion { get; } = dsoVersion;
}
