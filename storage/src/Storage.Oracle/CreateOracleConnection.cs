// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

/// <summary>
/// A delegate that creates an unopened <see cref="OracleConnection"/>.
/// Register as a keyed service matching the store's service key.
/// </summary>
public delegate OracleConnection CreateOracleConnection();
