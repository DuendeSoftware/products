// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.Common;
using Duende.Storage.Sqlite.Internal;

namespace Duende.Storage.Internal.Querying.Cursors;

public sealed class KeysetSqlBuilderTests : VerifyBase
{
    public KeysetSqlBuilderTests() : base()
    {
    }

    /// <summary>
    /// A minimal dialect that supports native NULLS FIRST/LAST ordering, standing in for
    /// PostgreSQL/Oracle (whose real dialect types aren't visible from this test assembly).
    /// </summary>
    private sealed class NativeNullsOrderingDialect : ISqlDialect
    {
        public string CaseInsensitiveLikeOperator => "ILIKE";
        public string TrueLiteral => "TRUE";
        public string FalseLiteral => "FALSE";
        public bool SupportsNullsOrdering => true;
        public string EscapeLikeWildcards(string value) => value;
        public void AddParameter(DbCommand command, string name, object value) => throw new NotSupportedException();
    }

    // SQLite emulates null ordering with CASE WHEN; the fake dialect stands in for PostgreSQL/Oracle's native NULLS FIRST/LAST.
    private static readonly ISqlDialect CaseDialect = new SqliteDialect();
    private static readonly ISqlDialect NativeDialect = new NativeNullsOrderingDialect();

    private const string SortColumn = "v.number_value";
    private const string IdColumn = "v.entity_id";
    private const string ValueParam = "@last_sort_value";
    private const string IdParam = "@last_id";

    private static readonly CursorPosition NonNullPosition =
        CursorPosition.ForKey(Guid.Parse("11111111-1111-1111-1111-111111111111"), CursorValueKind.Number, 42m);

    private static readonly CursorPosition NullPosition =
        CursorPosition.ForKey(Guid.Parse("22222222-2222-2222-2222-222222222222"), CursorValueKind.Null, null);

    private static readonly CursorPosition UnsortedPosition =
        CursorPosition.ForUnsortedKey(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private static string Describe(KeysetSqlBuilder builder, CursorDirection traversal, CursorPosition position) =>
        $"ORDER BY:\n{builder.OrderBy(traversal)}\n\n" +
        $"StrictlyAfter:\n{builder.StrictlyAfter(traversal, position, ValueParam, IdParam)}";

    // --- ASC, native dialect (PostgreSQL) ---

    [Fact]
    public async Task ascending_native_forward_non_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NonNullPosition));
    }

    [Fact]
    public async Task ascending_native_backward_non_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NonNullPosition));
    }

    [Fact]
    public async Task ascending_native_forward_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NullPosition));
    }

    [Fact]
    public async Task ascending_native_backward_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NullPosition));
    }

    [Fact]
    public async Task ascending_native_forward_start()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, CursorPosition.Start));
    }

    [Fact]
    public async Task ascending_native_backward_start()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, CursorPosition.Start));
    }

    // --- DESC, native dialect (PostgreSQL) ---

    [Fact]
    public async Task descending_native_forward_non_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NonNullPosition));
    }

    [Fact]
    public async Task descending_native_backward_non_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NonNullPosition));
    }

    [Fact]
    public async Task descending_native_forward_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NullPosition));
    }

    [Fact]
    public async Task descending_native_backward_null()
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NullPosition));
    }

    // --- ASC, CASE dialect (SQLite) ---

    [Fact]
    public async Task ascending_case_forward_non_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NonNullPosition));
    }

    [Fact]
    public async Task ascending_case_backward_non_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NonNullPosition));
    }

    [Fact]
    public async Task ascending_case_forward_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NullPosition));
    }

    [Fact]
    public async Task ascending_case_backward_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NullPosition));
    }

    [Fact]
    public async Task ascending_case_forward_start()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, CursorPosition.Start));
    }

    [Fact]
    public async Task ascending_case_backward_start()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, CursorPosition.Start));
    }

    // --- DESC, CASE dialect (SQLite) ---

    [Fact]
    public async Task descending_case_forward_non_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NonNullPosition));
    }

    [Fact]
    public async Task descending_case_backward_non_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NonNullPosition));
    }

    [Fact]
    public async Task descending_case_forward_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, NullPosition));
    }

    [Fact]
    public async Task descending_case_backward_null()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, SortDirection.Descending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, NullPosition));
    }

    // --- Unsorted ---

    [Fact]
    public async Task unsorted_forward()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, sortColumn: null, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, UnsortedPosition));
    }

    [Fact]
    public async Task unsorted_backward()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, sortColumn: null, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, UnsortedPosition));
    }

    [Fact]
    public async Task unsorted_forward_start()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, sortColumn: null, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Forward, CursorPosition.Start));
    }

    [Fact]
    public async Task unsorted_backward_start()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, sortColumn: null, SortDirection.Ascending, IdColumn);
        _ = await Verify(Describe(builder, CursorDirection.Backward, CursorPosition.Start));
    }

    // --- Backward ORDER BY is the exact key-by-key inverse of forward ---
    //
    // Each ORDER BY key is reversed in place (ASC<->DESC, NULLS LAST<->NULLS FIRST), and the keys'
    // order within the clause stays the same (sort key first, then the id tie-breaker): the backward
    // clause produces the exact reverse row sequence of the forward clause, key by key.

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public void backward_order_by_is_the_mirror_of_forward_native(SortDirection direction)
    {
        var builder = new KeysetSqlBuilder(NativeDialect, SortColumn, direction, IdColumn);
        var forwardDirection = direction == SortDirection.Ascending ? "ASC" : "DESC";
        var mirroredDirection = direction == SortDirection.Ascending ? "DESC" : "ASC";

        builder.OrderBy(CursorDirection.Forward).ShouldBe($"{SortColumn} {forwardDirection} NULLS LAST, {IdColumn} ASC");
        builder.OrderBy(CursorDirection.Backward).ShouldBe($"{SortColumn} {mirroredDirection} NULLS FIRST, {IdColumn} DESC");
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public void backward_order_by_is_the_mirror_of_forward_case(SortDirection direction)
    {
        var builder = new KeysetSqlBuilder(CaseDialect, SortColumn, direction, IdColumn);
        var forwardDirection = direction == SortDirection.Ascending ? "ASC" : "DESC";
        var mirroredDirection = direction == SortDirection.Ascending ? "DESC" : "ASC";

        builder.OrderBy(CursorDirection.Forward).ShouldBe(
            $"CASE WHEN {SortColumn} IS NULL THEN 1 ELSE 0 END ASC, {SortColumn} {forwardDirection}, {IdColumn} ASC");
        builder.OrderBy(CursorDirection.Backward).ShouldBe(
            $"CASE WHEN {SortColumn} IS NULL THEN 1 ELSE 0 END DESC, {SortColumn} {mirroredDirection}, {IdColumn} DESC");
    }

    [Fact]
    public void backward_order_by_is_the_mirror_of_forward_unsorted()
    {
        var builder = new KeysetSqlBuilder(CaseDialect, sortColumn: null, SortDirection.Ascending, IdColumn);
        builder.OrderBy(CursorDirection.Forward).ShouldBe($"{IdColumn} ASC");
        builder.OrderBy(CursorDirection.Backward).ShouldBe($"{IdColumn} DESC");
    }
}
