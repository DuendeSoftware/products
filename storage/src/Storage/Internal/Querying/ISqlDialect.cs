// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Defines SQL dialect-specific behavior for query building.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal interface ISqlDialect
{
    /// <summary>
    /// The LIKE operator for case-insensitive matching.
    /// PostgreSQL: "ILIKE", MSSQL: "LIKE" (relies on collation)
    /// </summary>
    string CaseInsensitiveLikeOperator { get; }

    /// <summary>
    /// Escapes wildcard characters (%, _) in LIKE patterns according to the dialect.
    /// PostgreSQL uses backslash escaping (\%), MSSQL uses bracket escaping ([%]).
    /// </summary>
    string EscapeLikeWildcards(string value);

    /// <summary>
    /// Gets the SQL ESCAPE clause suffix to append after LIKE expressions.
    /// SQLite requires an explicit ESCAPE clause (e.g. " ESCAPE '\'"), while PostgreSQL
    /// and SQL Server handle escaping natively and return an empty string.
    /// </summary>
    string LikeEscapeClause => "";

    /// <summary>
    /// Adds a parameter to the command, handling type-specific conversions.
    /// Implementations should handle DateTimeOffset, DateTime, and other types appropriately.
    /// </summary>
    void AddParameter(DbCommand command, string name, object value);

    /// <summary>
    /// Converts a field path GUID to the appropriate parameter value for the dialect.
    /// SQLite stores field paths as BLOBs (byte arrays), while other dialects use the native GUID type.
    /// </summary>
    object FieldPathToParameterValue(Guid fieldPathId) => fieldPathId;

    /// <summary>
    /// Gets the SQL literal for TRUE.
    /// PostgreSQL: "TRUE", MSSQL: "1=1"
    /// </summary>
    string TrueLiteral { get; }

    /// <summary>
    /// Gets the SQL literal for FALSE.
    /// PostgreSQL: "FALSE", MSSQL: "1=0"
    /// </summary>
    string FalseLiteral { get; }

    /// <summary>
    /// Quotes a SQL identifier (schema name or table name) using the dialect's quoting style.
    /// SQL Server uses square brackets ([identifier]), PostgreSQL uses double quotes ("identifier"),
    /// and SQLite returns the identifier unchanged.
    /// </summary>
    string QuoteIdentifier(string identifier) => identifier;
}
