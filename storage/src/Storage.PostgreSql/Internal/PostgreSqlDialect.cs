// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using Duende.Storage.Internal.Querying;
using Npgsql;
using NpgsqlTypes;

namespace Duende.Storage.PostgreSql.Internal;

/// <summary>
/// PostgreSQL-specific SQL dialect implementation.
/// </summary>
internal sealed class PostgreSqlDialect : ISqlDialect
{
    public string CaseInsensitiveLikeOperator => "ILIKE";

    public string TrueLiteral => "TRUE";

    public string FalseLiteral => "FALSE";

    public string EscapeLikeWildcards(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // PostgreSQL uses backslash as escape character
        // Replace backslash first to avoid double-escaping
        return value
            .Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%", "\\%", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "\\_", StringComparison.OrdinalIgnoreCase);
    }

    public void AddParameter(DbCommand command, string name, object value)
    {
        var npgsqlCommand = (NpgsqlCommand)command;

        // Handle DateTimeOffset and DateTime with explicit type
        if (value is DateTime dt)
        {
            _ = npgsqlCommand.Parameters.AddWithValue(name, NpgsqlDbType.TimestampTz, dt);
        }
        else if (value is DateTimeOffset dto)
        {
            _ = npgsqlCommand.Parameters.AddWithValue(name, NpgsqlDbType.TimestampTz, dto.UtcDateTime);
        }
        else
        {
            _ = npgsqlCommand.Parameters.AddWithValue(name, value);
        }
    }
}
