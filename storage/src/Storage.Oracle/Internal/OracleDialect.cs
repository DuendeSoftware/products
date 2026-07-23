// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using Duende.Storage.Internal.Querying;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Duende.Storage.Oracle.Internal;

/// <summary>
/// Oracle-specific SQL dialect implementation.
/// </summary>
internal sealed class OracleDialect : ISqlDialect
{
    // Oracle has no ILIKE. The store sets NLS_COMP=LINGUISTIC / NLS_SORT=BINARY_CI on the
    // session when opening a connection, which makes a plain LIKE case-insensitive.
    public string CaseInsensitiveLikeOperator => "LIKE";

    // Oracle requires a boolean predicate (it has no SQL boolean literal), so use 1=1 / 1=0.
    public string TrueLiteral => "1=1";

    public string FalseLiteral => "1=0";

    // Oracle LIKE requires an explicit ESCAPE clause to treat backslash as the escape character.
    public string LikeEscapeClause => " ESCAPE '\\'";

    public string EscapeLikeWildcards(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Oracle uses the ESCAPE character (backslash, see LikeEscapeClause) to escape wildcards.
        // Replace backslash first to avoid double-escaping.
        return value
            .Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%", "\\%", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "\\_", StringComparison.OrdinalIgnoreCase);
    }

    // Oracle folds unquoted identifiers to uppercase; quote the uppercased name so it matches
    // the objects created by the migration (which use uppercase unquoted identifiers).
    public string QuoteIdentifier(string identifier) =>
        $"\"{identifier.ToUpperInvariant().Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    public object FieldPathToParameterValue(Guid fieldPathId) => OracleGuidConverter.ToRaw(fieldPathId);

    public void AddParameter(DbCommand command, string name, object value)
    {
        var oracleCommand = (OracleCommand)command;
        oracleCommand.BindByName = true;

        var parameterName = name.TrimStart('@', ':');
        var parameter = new OracleParameter { ParameterName = parameterName };

        switch (value)
        {
            case DateTimeOffset dto:
                parameter.OracleDbType = OracleDbType.TimeStampTZ;
                parameter.Value = new OracleTimeStampTZ(dto.UtcDateTime, "UTC");
                break;
            case DateTime dt:
                parameter.OracleDbType = OracleDbType.TimeStampTZ;
                parameter.Value = new OracleTimeStampTZ(DateTime.SpecifyKind(dt, DateTimeKind.Utc), "UTC");
                break;
            case Guid guid:
                parameter.OracleDbType = OracleDbType.Raw;
                parameter.Value = OracleGuidConverter.ToRaw(guid);
                break;
            case byte[] bytes:
                parameter.OracleDbType = OracleDbType.Raw;
                parameter.Value = bytes;
                break;
            case bool b:
                parameter.OracleDbType = OracleDbType.Int32;
                parameter.Value = b ? 1 : 0;
                break;
            case null:
                parameter.Value = DBNull.Value;
                break;
            default:
                parameter.Value = value;
                break;
        }

        _ = oracleCommand.Parameters.Add(parameter);
    }
}
