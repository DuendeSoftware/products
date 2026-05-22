// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Builder;

/// <summary>
/// Delegate that resolves the registered CLR type for a given DSO version.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="version">The DSO version to look up.</param>
/// <returns>The registered CLR type.</returns>
public delegate Type GetRegisteredTypeForDso(DataStorageObjectVersion version);
