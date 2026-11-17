// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.Sorting;

namespace Duende.Storage.Internal.Querying.Cursors;

public sealed class CursorPageAssemblerTests
{
    private static readonly EntityType EntityType = new(1, "Test");
    private static readonly SortParameter NumberSortAsc = new(new NumberField("rank"));

    private sealed record Row(Guid Id, decimal? Rank);

    private static (Guid Id, object? SortValue) Key(Row row) => (row.Id, row.Rank);

    private static Row MakeRow(decimal rank) => new(Guid.NewGuid(), rank);

    [Fact]
    public void forward_page_with_extra_row_sets_has_more_data_from_same_side()
    {
        var rows = new[] { MakeRow(1), MakeRow(2), MakeRow(3) }; // pageSize=2, 1 extra
        var result = CursorPageAssembler.Assemble(
            rows, pageSize: 2, CursorDirection.Forward, CursorPosition.Start,
            EntityType, NumberSortAsc, Key);

        result.ItemsInRequestedOrder.Count.ShouldBe(2);
        result.ItemsInRequestedOrder[0].ShouldBe(rows[0]);
        result.ItemsInRequestedOrder[1].ShouldBe(rows[1]);
        result.HasMoreData.ShouldBeTrue();

        // Only a forward traversal starting from Start infers HasPreviousData = false.
        result.HasPreviousData.ShouldBeFalse();
    }

    [Fact]
    public void forward_page_from_a_key_position_infers_has_previous_data_true()
    {
        var rows = new[] { MakeRow(1), MakeRow(2) }; // pageSize=2, no extra
        var position = CursorPosition.ForKey(Guid.NewGuid(), CursorValueKind.Number, 0m);
        var result = CursorPageAssembler.Assemble(
            rows, pageSize: 2, CursorDirection.Forward, position,
            EntityType, NumberSortAsc, Key);

        result.HasMoreData.ShouldBeFalse();

        // Inferred: any non-Start position implies HasPreviousData = true.
        result.HasPreviousData.ShouldBeTrue();
    }

    [Fact]
    public void backward_rows_are_reversed_into_requested_order()
    {
        // Fetched in backward traversal order (descending), with one extra row for HasPreviousData.
        var rows = new[] { MakeRow(5), MakeRow(4), MakeRow(3) }; // pageSize=2
        var position = CursorPosition.ForKey(Guid.NewGuid(), CursorValueKind.Number, 6m);
        var result = CursorPageAssembler.Assemble(
            rows, pageSize: 2, CursorDirection.Backward, position,
            EntityType, NumberSortAsc, Key);

        // Requested order (ascending by rank): [4, 5]. The dropped extra row (rank 3) must not appear.
        result.ItemsInRequestedOrder.Count.ShouldBe(2);
        result.ItemsInRequestedOrder[0].ShouldBe(rows[1]); // rank 4
        result.ItemsInRequestedOrder[1].ShouldBe(rows[0]); // rank 5
        result.HasPreviousData.ShouldBeTrue();

        // Inferred: backward traversal is never Start/forward, so HasMoreData is inferred true here,
        // even though this page has no "after the page" row of its own.
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public void backward_page_reaching_the_first_rows_has_no_previous_data()
    {
        // Backward from a key position, page exactly fills with no extra row to prove more exist
        // beyond it: HasPreviousData is exact (false, there's no earlier row), HasMoreData is
        // inferred true (backward traversal is never Start/forward, so it's conservatively true).
        var rows = new[] { MakeRow(2), MakeRow(1) }; // pageSize=2, no extra, requested order [1, 2]
        var position = CursorPosition.ForKey(Guid.NewGuid(), CursorValueKind.Number, 3m);
        var result = CursorPageAssembler.Assemble(
            rows, pageSize: 2, CursorDirection.Backward, position,
            EntityType, NumberSortAsc, Key);

        result.HasPreviousData.ShouldBeFalse();

        // Backward from Start is one of the "otherwise true" cases, even without an opposite-side probe.
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public void backward_page_from_start_is_empty_and_infers_has_more_data_true()
    {
        // An empty page reached by backward traversal from the request's Start position: conservative
        // inference means HasMoreData stays true (there is no probe to disprove it), even though in
        // this scenario no rows exist on that side.
        var position = CursorPosition.Start;
        var result = CursorPageAssembler.Assemble(
            Array.Empty<Row>(), pageSize: 2, CursorDirection.Backward, position,
            EntityType, NumberSortAsc, Key);

        result.ItemsInRequestedOrder.ShouldBeEmpty();
        result.HasPreviousData.ShouldBeFalse();
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public void forward_tokens_continue_from_the_last_and_first_item()
    {
        var rows = new[] { MakeRow(1), MakeRow(2) };
        var result = CursorPageAssembler.Assemble(
            rows, pageSize: 2, CursorDirection.Forward, CursorPosition.Start,
            EntityType, NumberSortAsc, Key);

        var next = CursorToken.Decode(result.Next, NumberSortAsc, EntityType);
        var previous = CursorToken.Decode(result.Previous, NumberSortAsc, EntityType);

        next.Direction.ShouldBe(CursorDirection.Forward);
        next.Position.Id.ShouldBe(rows[1].Id);
        next.Position.Value.ShouldBe(2m);

        previous.Direction.ShouldBe(CursorDirection.Backward);
        previous.Position.Id.ShouldBe(rows[0].Id);
        previous.Position.Value.ShouldBe(1m);
    }

    [Fact]
    public void empty_forward_page_from_start_carries_the_start_position_in_both_tokens()
    {
        var result = CursorPageAssembler.Assemble(
            Array.Empty<Row>(), pageSize: 2, CursorDirection.Forward, CursorPosition.Start,
            EntityType, NumberSortAsc, Key);

        result.ItemsInRequestedOrder.ShouldBeEmpty();
        result.HasMoreData.ShouldBeFalse();
        result.HasPreviousData.ShouldBeFalse();

        var next = CursorToken.Decode(result.Next, NumberSortAsc, EntityType);
        var previous = CursorToken.Decode(result.Previous, NumberSortAsc, EntityType);

        next.Direction.ShouldBe(CursorDirection.Forward);
        next.Position.Kind.ShouldBe(CursorPositionKind.Start);
        previous.Direction.ShouldBe(CursorDirection.Backward);
        previous.Position.Kind.ShouldBe(CursorPositionKind.Start);
    }

    [Fact]
    public void empty_page_from_a_key_position_carries_that_position_in_both_tokens()
    {
        var id = Guid.NewGuid();
        var position = CursorPosition.ForKey(id, CursorValueKind.Number, 7m);
        var result = CursorPageAssembler.Assemble(
            Array.Empty<Row>(), pageSize: 2, CursorDirection.Forward, position,
            EntityType, NumberSortAsc, Key);

        var next = CursorToken.Decode(result.Next, NumberSortAsc, EntityType);
        var previous = CursorToken.Decode(result.Previous, NumberSortAsc, EntityType);

        next.Direction.ShouldBe(CursorDirection.Forward);
        next.Position.Id.ShouldBe(id);
        next.Position.Value.ShouldBe(7m);

        previous.Direction.ShouldBe(CursorDirection.Backward);
        previous.Position.Id.ShouldBe(id);
        previous.Position.Value.ShouldBe(7m);

        // Inferred: a forward traversal from a key position implies HasPreviousData = true.
        result.HasPreviousData.ShouldBeTrue();
        result.HasMoreData.ShouldBeFalse();
    }
}
