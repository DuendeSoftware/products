// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using Duende.Storage.Internal.Querying;
using Microsoft.Data.Sqlite;

namespace Duende.Storage.Sqlite.Internal;

/// <summary>
/// SQLite-specific SQL dialect implementation.
/// </summary>
internal sealed class SqliteDialect : ISqlDialect
{
    public string CaseInsensitiveLikeOperator => "LIKE";

    public string LikeEscapeClause => " ESCAPE '\\'";

    public string TrueLiteral => "1";

    public string FalseLiteral => "0";

    public string EscapeLikeWildcards(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // SQLite uses backslash as escape character (like PostgreSQL)
        // Replace backslash first to avoid double-escaping
        return value
            .Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%", "\\%", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "\\_", StringComparison.OrdinalIgnoreCase);
    }

    public object FieldPathToParameterValue(Guid fieldPathId) => fieldPathId.ToByteArray();

    public void AddParameter(DbCommand command, string name, object value)
    {
        var sqliteCommand = (SqliteCommand)command;

        _ = value switch
        {
            DateTimeOffset dto => sqliteCommand.Parameters.AddWithValue(name, dto.UtcDateTime.ToString("O")),
            DateTime dt => sqliteCommand.Parameters.AddWithValue(name, new DateTimeOffset(dt, TimeSpan.Zero).UtcDateTime.ToString("O")),
            Guid guid => sqliteCommand.Parameters.AddWithValue(name, guid.ToString()),
            bool b => sqliteCommand.Parameters.AddWithValue(name, b ? 1 : 0),
            _ => sqliteCommand.Parameters.AddWithValue(name, value)
        };
    }
}
