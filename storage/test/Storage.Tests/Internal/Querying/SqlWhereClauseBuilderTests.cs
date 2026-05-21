// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Sqlite.Internal;
using Microsoft.Data.Sqlite;

namespace Duende.Storage.Internal.Querying;

public sealed class SqlWhereClauseBuilderTests : VerifyBase
{
    public SqlWhereClauseBuilderTests() : base()
    {
    }

    private static readonly ISqlDialect Dialect = new SqliteDialect();
    private const string Schema = "main";

    private static (SqlWhereClauseBuilder Builder, SqliteCommand Command) CreateBuilder()
    {
        var command = new SqliteCommand();
        var builder = new SqlWhereClauseBuilder(Schema, command, Dialect);
        return (builder, command);
    }

    private static string FormatValue(object? value) => value switch
    {
        byte[] bytes when bytes.Length == 16 => new Guid(bytes).ToString(),
        _ => value?.ToString() ?? "NULL"
    };

    private static string BuildAndDescribeParameters(SqlWhereClauseBuilder builder, SqliteCommand command, IQueryExpression expression)
    {
        var sql = builder.BuildWhereClause(expression);
        var parameters = command.Parameters
            .Cast<SqliteParameter>()
            .Select(p => $"{p.ParameterName} = {FormatValue(p.Value)}")
            .ToList();
        return $"SQL:\n{sql}\n\nParameters:\n{string.Join("\n", parameters)}";
    }

    [Fact]
    public async Task all_expression()
    {
        var (builder, command) = CreateBuilder();
        var result = BuildAndDescribeParameters(builder, command, AllExpression.Instance);
        _ = await Verify(result);
    }

    [Fact]
    public async Task equal_expression_string_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("userName");
        var expression = field.Equals("alice");
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task equal_expression_number_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("age");
        var expression = field.Equals(42m);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task equal_expression_system_field_created()
    {
        var (builder, command) = CreateBuilder();
        var field = new DateTimeField(SystemFields.Created);
        var expression = new EqualExpression(field, new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task contains_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("displayName");
        var expression = field.Contains("alice");
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task contains_expression_with_wildcards()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("displayName");
        var expression = field.Contains("al%ice_test");
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task starts_with_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("userName");
        var expression = field.StartsWith("ali");
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task ends_with_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("email");
        var expression = field.EndsWith("@example.com");
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task greater_than_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("age");
        var expression = field.GreaterThan(18m);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task greater_than_expression_system_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new DateTimeField(SystemFields.LastUpdated);
        var expression = new GreaterThanExpression(field, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task less_than_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("score");
        var expression = field.LessThan(100m);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task greater_or_equal_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("age");
        var expression = field.GreaterOrEqual(18m);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task less_or_equal_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("age");
        var expression = field.LessOrEqual(65m);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task between_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("age");
        var expression = field.Between(18m, 65m);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task between_expression_system_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new DateTimeField(SystemFields.Created);
        var expression = new BetweenExpression(field,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task in_expression_string_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("status");
        var expression = field.In(["active", "pending"]);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task in_expression_number_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("priority");
        var expression = field.In([1m, 2m, 3m]);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task in_expression_empty_list()
    {
        var (builder, command) = CreateBuilder();
        var field = new NumberField("priority");
        var expression = field.In([]);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task in_expression_system_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new DateTimeField(SystemFields.Created);
        var expression = new InExpression(field, new[]
        {
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task present_expression_scalar_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("email");
        var expression = new PresentExpression(field);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task present_expression_array_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringArrayField("emails");
        var expression = new PresentExpression(field);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task present_expression_system_field()
    {
        var (builder, command) = CreateBuilder();
        var field = new DateTimeField(SystemFields.Created);
        var expression = new PresentExpression(field);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task array_contains_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringArrayField("emails");
        var expression = field.Contains("alice@example.com");
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task not_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("status");
        var inner = field.Equals("inactive");
        var expression = new NotExpression(inner);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task and_expression()
    {
        var (builder, command) = CreateBuilder();
        var nameField = new StringField("userName");
        var ageField = new NumberField("age");
        var expression = new AndExpression([nameField.Equals("alice"), ageField.GreaterThan(18m)]);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task or_expression()
    {
        var (builder, command) = CreateBuilder();
        var field = new StringField("status");
        var expression = new OrExpression([field.Equals("active"), field.Equals("pending")]);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task array_filter_expression_single_condition()
    {
        var (builder, command) = CreateBuilder();
        var typeField = new StringField("type");
        var expression = new ArrayFilterExpression("emails", typeField.Equals("work"));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task array_filter_expression_and_conditions()
    {
        var (builder, command) = CreateBuilder();
        var typeField = new StringField("type");
        var valueField = new StringField("value");
        var filter = new AndExpression([typeField.Equals("work"), valueField.Contains("@example.com")]);
        var expression = new ArrayFilterExpression("emails", filter);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task array_filter_expression_or_conditions()
    {
        var (builder, command) = CreateBuilder();
        var typeField = new StringField("type");
        var filter = new OrExpression([typeField.Equals("work"), typeField.Equals("home")]);
        var expression = new ArrayFilterExpression("emails", filter);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }
}
