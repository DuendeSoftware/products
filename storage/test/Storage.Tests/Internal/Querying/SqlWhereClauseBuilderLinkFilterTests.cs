// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Sqlite.Internal;
using Microsoft.Data.Sqlite;

namespace Duende.Storage.Internal.Querying;

public sealed class SqlWhereClauseBuilderLinkFilterTests : VerifyBase
{
    public SqlWhereClauseBuilderLinkFilterTests() : base()
    {
    }

    private static readonly ISqlDialect Dialect = new SqliteDialect();
    private const string Schema = "main";

    private static readonly EntityType ResourceType = new(9910, "LinkFilterTestResource");
    private static readonly EntityType ScopeType = new(9911, "LinkFilterTestScope");

    private static readonly LinkDefinition CascadeDefinition = new()
    {
        Left = ResourceType,
        Right = ScopeType,
        Link = new LinkType(9912, "LinkFilterTestCascadeLink")
    };

    private static readonly LinkDefinition RestrictedDefinition = CascadeDefinition with
    {
        Link = new LinkType(9913, "LinkFilterTestRestrictedLink"),
        OnRightDelete = LinkDeleteBehavior.Restrict
    };

    private static (SqlWhereClauseBuilder Builder, SqliteCommand Command) CreateBuilder(EntityType? queriedEntityType = null)
    {
        var command = new SqliteCommand();
        var builder = new SqlWhereClauseBuilder(Schema, command, Dialect, tableNames: null, queriedEntityType);
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
    public async Task link_exists_left_to_right_no_inner_filter()
    {
        var (builder, command) = CreateBuilder();
        var expression = LinkFilter.ToRight(CascadeDefinition);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_right_to_left_no_inner_filter()
    {
        var (builder, command) = CreateBuilder();
        var expression = LinkFilter.ToLeft(CascadeDefinition);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_left_to_right_with_inner_filter()
    {
        var (builder, command) = CreateBuilder();
        var nameField = new StringField("name");
        var expression = LinkFilter.ToRight(CascadeDefinition, nameField.Equals("a"));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_right_to_left_with_inner_filter()
    {
        var (builder, command) = CreateBuilder();
        var nameField = new StringField("name");
        var expression = LinkFilter.ToLeft(CascadeDefinition, nameField.Equals("a"));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_restricted_definition_routes_to_the_same_link_table()
    {
        var (builder, command) = CreateBuilder();
        var expression = LinkFilter.ToRight(RestrictedDefinition);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_inner_filter_resolves_system_field_to_linked_alias()
    {
        var (builder, command) = CreateBuilder();
        var createdField = new DateTimeField(SystemFields.Created);
        var expression = LinkFilter.ToRight(CascadeDefinition, new EqualExpression(createdField, new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_inside_and_expression()
    {
        var (builder, command) = CreateBuilder();
        var nameField = new StringField("name");
        var expression = new AndExpression([nameField.Equals("alice"), LinkFilter.ToRight(CascadeDefinition)]);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_inside_or_expression()
    {
        var (builder, command) = CreateBuilder();
        var expression = new OrExpression([LinkFilter.ToRight(CascadeDefinition), LinkFilter.ToRight(RestrictedDefinition)]);
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_inside_not_expression()
    {
        var (builder, command) = CreateBuilder();
        var expression = Query.Not(LinkFilter.ToRight(CascadeDefinition));
        var result = BuildAndDescribeParameters(builder, command, expression);
        _ = await Verify(result);
    }

    [Fact]
    public async Task link_exists_nested_two_levels()
    {
        var (builder, command) = CreateBuilder();
        var innerExpression = LinkFilter.ToRight(CascadeDefinition);
        var outerExpression = LinkFilter.ToLeft(CascadeDefinition, innerExpression);
        var result = BuildAndDescribeParameters(builder, command, outerExpression);
        _ = await Verify(result);
    }

    [Fact]
    public void link_exists_validates_queried_entity_type_matches_left_for_left_to_right()
    {
        var (builder, _) = CreateBuilder(ScopeType);
        var expression = LinkFilter.ToRight(CascadeDefinition);

        var ex = Should.Throw<InvalidOperationException>(() => builder.BuildWhereClause(expression));
        ex.Message.ShouldContain(CascadeDefinition.Link.Name);
    }

    [Fact]
    public void link_exists_validates_queried_entity_type_matches_right_for_right_to_left()
    {
        var (builder, _) = CreateBuilder(ResourceType);
        var expression = LinkFilter.ToLeft(CascadeDefinition);

        _ = Should.Throw<InvalidOperationException>(() => builder.BuildWhereClause(expression));
    }

    [Fact]
    public void link_exists_passes_validation_when_queried_entity_type_matches()
    {
        var (builder, _) = CreateBuilder(ResourceType);
        var expression = LinkFilter.ToRight(CascadeDefinition);

        _ = Should.NotThrow(() => builder.BuildWhereClause(expression));
    }

    [Fact]
    public void link_exists_rejected_inside_array_filter()
    {
        var (builder, _) = CreateBuilder();
        var expression = new ArrayFilterExpression("emails", LinkFilter.ToRight(CascadeDefinition));

        _ = Should.Throw<NotSupportedException>(() => builder.BuildWhereClause(expression));
    }

    /// <summary>
    /// Builds a chain of <paramref name="levels"/> nested LinkExistsExpressions, alternating direction level by
    /// level so the queried/linked entity types stay consistent with <see cref="CascadeDefinition"/> throughout.
    /// </summary>
    private static IQueryFilterExpression BuildNestedChain(int levels)
    {
        IQueryFilterExpression? current = null;
        for (var i = levels - 1; i >= 0; i--)
        {
            var direction = i % 2 == 0 ? LinkJoinDirection.LeftToRight : LinkJoinDirection.RightToLeft;
            current = LinkFilter.Exists(CascadeDefinition, direction, current);
        }

        return current!;
    }

    [Fact]
    public void link_exists_nesting_depth_cap_is_enforced()
    {
        var (builder, _) = CreateBuilder();

        // 5 nested LinkExistsExpressions exceed the maximum depth of 4.
        var expression = BuildNestedChain(5);

        _ = Should.Throw<InvalidOperationException>(() => builder.BuildWhereClause(expression));
    }

    [Fact]
    public void link_exists_nesting_up_to_max_depth_is_allowed()
    {
        var (builder, _) = CreateBuilder();

        // 4 nested LinkExistsExpressions is exactly the maximum depth.
        var expression = BuildNestedChain(4);

        _ = Should.NotThrow(() => builder.BuildWhereClause(expression));
    }
}
