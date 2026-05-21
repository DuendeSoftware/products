// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Data.SqlClient;

namespace Duende.Storage.MsSql;

/// <summary>
/// A delegate that creates an unopened <see cref="SqlConnection"/>.
/// Register as a keyed service matching the store's service key.
/// </summary>
public delegate SqlConnection CreateSqlConnection();
