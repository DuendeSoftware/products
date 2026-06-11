// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using Duende.Storage.Internal.Querying;
using Microsoft.Data.SqlClient;

namespace Duende.Storage.MsSql.Internal;

/// <summary>
/// SQL Server-specific SQL dialect implementation.
/// </summary>
internal sealed class MsSqlDialect : ISqlDialect
{
    public string CaseInsensitiveLikeOperator => "LIKE";

    public string TrueLiteral => "1=1";

    public string FalseLiteral => "1=0";

    public string EscapeLikeWildcards(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // SQL Server uses brackets to escape special characters
        return value
            .Replace("[", "[[]", StringComparison.OrdinalIgnoreCase)
            .Replace("%", "[%]", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "[_]", StringComparison.OrdinalIgnoreCase);
    }

    public string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    public void AddParameter(DbCommand command, string name, object value)
    {
        var sqlCommand = (SqlCommand)command;

        // Handle DateTimeOffset with explicit type
        if (value is DateTimeOffset dto)
        {
            var param = sqlCommand.Parameters.AddWithValue(name, dto);
            param.SqlDbType = SqlDbType.DateTimeOffset;
        }
        else if (value is DateTime dt)
        {
            // Convert to DateTimeOffset assuming UTC
            var param = sqlCommand.Parameters.AddWithValue(name, new DateTimeOffset(dt, TimeSpan.Zero));
            param.SqlDbType = SqlDbType.DateTimeOffset;
        }
        else
        {
            _ = sqlCommand.Parameters.AddWithValue(name, value);
        }
    }
}
