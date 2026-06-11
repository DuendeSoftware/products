// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Microsoft.Data.Sqlite;

namespace Duende.Storage.Internal.Querying;

public sealed class SqlWhereClauseBuilderIdentifierQuotingTests : VerifyBase
{
    public SqlWhereClauseBuilderIdentifierQuotingTests() : base()
    {
    }

    [Fact]
    public async Task reserved_word_schema_is_quoted_in_exists_subquery()
    {
        var command = new SqliteCommand();
        var builder = new SqlWhereClauseBuilder("identity", command, new BracketQuotingDialect());
        var field = new StringField("userName");
        var expression = new AndExpression([field.Equals("alice"), new StringField("provider").Equals("local")]);

        var sql = builder.BuildWhereClause(expression);
        var parameters = command.Parameters
            .Cast<SqliteParameter>()
            .Select(p => $"{p.ParameterName} = {FormatValue(p.Value)}")
            .ToList();
        var result = $"SQL:\n{sql}\n\nParameters:\n{string.Join("\n", parameters)}";
        _ = await Verify(result);
    }

    [Fact]
    public async Task reserved_word_schema_is_quoted_in_array_filter_joins()
    {
        var command = new SqliteCommand();
        var builder = new SqlWhereClauseBuilder("identity", command, new BracketQuotingDialect());
        var typeField = new StringField("type");
        var valueField = new StringField("value");
        var filter = new AndExpression([typeField.Equals("work"), valueField.Contains("@example.com")]);
        var expression = new ArrayFilterExpression("emails", filter);

        var sql = builder.BuildWhereClause(expression);
        var parameters = command.Parameters
            .Cast<SqliteParameter>()
            .Select(p => $"{p.ParameterName} = {FormatValue(p.Value)}")
            .ToList();
        var result = $"SQL:\n{sql}\n\nParameters:\n{string.Join("\n", parameters)}";
        _ = await Verify(result);
    }

    [Fact]
    public async Task non_reserved_schema_is_still_quoted()
    {
        var command = new SqliteCommand();
        var builder = new SqlWhereClauseBuilder("dbo", command, new BracketQuotingDialect());
        var field = new StringField("userName");
        var expression = field.Equals("alice");

        var sql = builder.BuildWhereClause(expression);
        var parameters = command.Parameters
            .Cast<SqliteParameter>()
            .Select(p => $"{p.ParameterName} = {FormatValue(p.Value)}")
            .ToList();
        var result = $"SQL:\n{sql}\n\nParameters:\n{string.Join("\n", parameters)}";
        _ = await Verify(result);
    }

    [Fact]
    public async Task postgresql_dialect_quotes_with_double_quotes()
    {
        var command = new SqliteCommand();
        var builder = new SqlWhereClauseBuilder("identity", command, new DoubleQuoteDialect());
        var field = new StringField("userName");
        var expression = new AndExpression([field.Equals("alice"), new StringField("provider").Equals("local")]);

        var sql = builder.BuildWhereClause(expression);
        var parameters = command.Parameters
            .Cast<SqliteParameter>()
            .Select(p => $"{p.ParameterName} = {FormatValue(p.Value)}")
            .ToList();
        var result = $"SQL:\n{sql}\n\nParameters:\n{string.Join("\n", parameters)}";
        _ = await Verify(result);
    }

    private static string FormatValue(object? value) => value switch
    {
        byte[] bytes when bytes.Length == 16 => new Guid(bytes).ToString(),
        _ => value?.ToString() ?? "NULL"
    };

    /// <summary>
    /// Test dialect that mimics SQL Server bracket quoting behavior.
    /// Uses SqliteCommand for simplicity (no actual SQL execution needed).
    /// </summary>
    private sealed class BracketQuotingDialect : ISqlDialect
    {
        public string CaseInsensitiveLikeOperator => "LIKE";
        public string TrueLiteral => "1=1";
        public string FalseLiteral => "1=0";

        public string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

        public string EscapeLikeWildcards(string value) => value
            .Replace("[", "[[]", StringComparison.OrdinalIgnoreCase)
            .Replace("%", "[%]", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "[_]", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// Test dialect that mimics PostgreSQL double-quote quoting behavior.
    /// Uses SqliteCommand for simplicity (no actual SQL execution needed).
    /// </summary>
    private sealed class DoubleQuoteDialect : ISqlDialect
    {
        public string CaseInsensitiveLikeOperator => "ILIKE";
        public string TrueLiteral => "TRUE";
        public string FalseLiteral => "FALSE";

        public string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        public string EscapeLikeWildcards(string value) => value
            .Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase)
            .Replace("%", "\\%", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "\\_", StringComparison.OrdinalIgnoreCase);

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
}
