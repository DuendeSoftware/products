// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Reflection;
using Duende.IdentityServer.Internal;
using Models = Duende.IdentityServer.Models;
using StorageContinuationToken = Duende.Storage.Internal.Pagination.ContinuationToken;

namespace IdentityServer.UnitTests.Internal;

/// <summary>
/// Builds Duende.Storage result instances for tests. SaveResult&lt;TId&gt; and GetResult&lt;T&gt;
/// have internal constructors and internal setters (callers only ever receive one from Storage
/// itself), so tests build them through reflection instead of a public API.
/// </summary>
internal static class StorageTestResults
{
    public static Duende.Storage.Internal.SaveResult<TId> Success<TId>(TId id, int version) where TId : notnull
    {
        var result = Create<Duende.Storage.Internal.SaveResult<TId>>();
        SetProperty(result, nameof(Duende.Storage.Internal.SaveResult<TId>.IsSuccess), true);
        SetProperty(result, nameof(Duende.Storage.Internal.SaveResult<TId>.Id), id);
        SetProperty(result, nameof(Duende.Storage.Internal.SaveResult<TId>.Version), (Duende.Storage.Internal.DataVersion)version);
        return result;
    }

    public static Duende.Storage.Internal.SaveResult<TId> Failure<TId>(params Duende.Storage.Internal.ErrorResult[] errors) where TId : notnull
    {
        var result = Create<Duende.Storage.Internal.SaveResult<TId>>();
        SetProperty(result, nameof(Duende.Storage.Internal.SaveResult<TId>.IsSuccess), false);
        SetProperty(result, nameof(Duende.Storage.Internal.SaveResult<TId>.Errors), (IReadOnlyList<Duende.Storage.Internal.ErrorResult>)errors);
        return result;
    }

    public static Duende.Storage.Internal.GetResult<T> Found<T>(T item, int version)
    {
        var result = Create<Duende.Storage.Internal.GetResult<T>>();
        SetProperty(result, nameof(Duende.Storage.Internal.GetResult<T>.Found), true);
        SetProperty(result, nameof(Duende.Storage.Internal.GetResult<T>.Item), item);
        SetProperty(result, nameof(Duende.Storage.Internal.GetResult<T>.Version), (Duende.Storage.Internal.DataVersion)version);
        return result;
    }

    public static Duende.Storage.Internal.GetResult<T> NotFound<T>()
    {
        var result = Create<Duende.Storage.Internal.GetResult<T>>();
        SetProperty(result, nameof(Duende.Storage.Internal.GetResult<T>.Found), false);
        return result;
    }

    private static T Create<T>() =>
        (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static void SetProperty<T>(T target, string propertyName, object? value)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{typeof(T)} has no property '{propertyName}'.");
        property.SetValue(target, value);
    }
}

/// <summary>
/// Tests the one-way (request) and two-way (result) conversions between the public query/result
/// clones in <see cref="Duende.IdentityServer.Models"/> and the Duende.Storage types they're built
/// from, in <see cref="StorageMapping"/>.
/// </summary>
public class StorageMappingTests
{
    public enum TestSortField
    {
        Name,
        CreatedAt
    }

    // === DataRange: each paging mode, both ways ===

    [Fact]
    public void DataRange_continuation_token_round_trips()
    {
        var model = Models.DataRange.FromContinuationToken((Models.ContinuationToken)"abc", 10);

        var storage = model.ToStorage();

        storage.ShouldNotBeNull();
        storage.TokenValue.ShouldNotBeNull();
        storage.TokenValue.Start.Value.ShouldBe("abc");
        storage.TokenValue.Size.Value.ShouldBe(10);

        var roundTripped = storage.ToModel();
        roundTripped.TokenValue.ShouldNotBeNull();
        roundTripped.TokenValue!.Start.Value.ShouldBe("abc");
        roundTripped.TokenValue.Size.Value.ShouldBe(10);
        roundTripped.OffsetValue.ShouldBeNull();
        roundTripped.PageValue.ShouldBeNull();
    }

    [Fact]
    public void DataRange_empty_continuation_token_maps_to_beginning_start()
    {
        // DataRange.FromContinuationToken(null) stores ContinuationToken.Beginning as the start.
        var model = Models.DataRange.FromContinuationToken((Models.ContinuationToken?)null);

        var storage = model.ToStorage();

        storage.ShouldNotBeNull();
        storage.TokenValue.ShouldNotBeNull();
        storage.TokenValue.Start.ShouldBe(StorageContinuationToken.Beginning);
    }

    [Fact]
    public void DataRange_offset_round_trips()
    {
        var model = Models.DataRange.FromOffset(42, 5);

        var storage = model.ToStorage();

        storage.ShouldNotBeNull();
        storage.OffsetValue.ShouldNotBeNull();
        storage.OffsetValue.Skip.Value.ShouldBe(42);
        storage.OffsetValue.Take.Value.ShouldBe(5);

        var roundTripped = storage.ToModel();
        roundTripped.OffsetValue.ShouldNotBeNull();
        roundTripped.OffsetValue!.Skip.Value.ShouldBe(42);
        roundTripped.OffsetValue.Take.Value.ShouldBe(5);
        roundTripped.TokenValue.ShouldBeNull();
        roundTripped.PageValue.ShouldBeNull();
    }

    [Fact]
    public void DataRange_page_round_trips()
    {
        var model = Models.DataRange.FromPage(3, 20);

        var storage = model.ToStorage();

        storage.ShouldNotBeNull();
        storage.PageValue.ShouldNotBeNull();
        storage.PageValue.Page.Value.ShouldBe(3);
        storage.PageValue.PageSize.Value.ShouldBe(20);

        var roundTripped = storage.ToModel();
        roundTripped.PageValue.ShouldNotBeNull();
        roundTripped.PageValue!.Page.Value.ShouldBe(3);
        roundTripped.PageValue.PageSize.Value.ShouldBe(20);
        roundTripped.TokenValue.ShouldBeNull();
        roundTripped.OffsetValue.ShouldBeNull();
    }

    [Fact]
    public void Null_DataRange_maps_to_null_so_storage_picks_its_default()
    {
        Models.DataRange? model = null;

        model.ToStorage().ShouldBeNull();
    }

    // === FilterBy: typed value and search expression ===

    [Fact]
    public void FilterBy_with_typed_value_rebuilds_the_typed_storage_filter()
    {
        var filter = Models.FilterBy.Filter("my-filter-value");

        var storage = filter.ToStorage();

        storage.ShouldNotBeNull();
        storage!.FilterValue.ShouldBe("my-filter-value");
    }

    [Fact]
    public void FilterBy_with_search_expression_rebuilds_via_the_public_factory()
    {
        Models.FilterBy<string> filter = Models.SearchExpression.Create("displayName eq \"Engineers\"");

        var storage = filter.ToStorage();

        storage.ShouldNotBeNull();
        storage!.FilterValue.ShouldBeNull();
    }

    [Fact]
    public void Null_FilterBy_maps_to_null()
    {
        Models.FilterBy<string>? filter = null;

        filter.ToStorage().ShouldBeNull();
    }

    [Fact]
    public void Nested_search_expression_converts_via_the_standalone_helper()
    {
        // Covers e.g. GroupFilter.SearchExpression: a filter DTO that carries its own
        // SearchExpression property directly, not through FilterBy.
        var expression = Models.SearchExpression.Create("displayName eq \"Engineers\"");

        var storage = expression.ToStorage();

        storage.ShouldNotBeNull();
        storage.Value.ShouldBe("displayName eq \"Engineers\"");
    }

    // === SortBy: attribute and typed field ===

    [Fact]
    public void SortBy_attribute_converts_with_its_direction()
    {
        Models.SortBy sort = Models.SortBy.Attribute("displayName", Models.SortDirection.Descending);

        var storage = sort.ToStorage();

        storage.ShouldBeOfType<Duende.Storage.Internal.Querying.SortBy.SortByAttributeCode>();
        var attributeSort = (Duende.Storage.Internal.Querying.SortBy.SortByAttributeCode)storage;
        attributeSort.Code.ShouldBe("displayName");
        attributeSort.Direction.ShouldBe(Duende.Storage.Internal.Querying.SortDirection.Descending);
    }

    [Fact]
    public void SortBy_typed_field_converts_with_its_direction()
    {
        var sort = Models.SortBy.Field(TestSortField.CreatedAt, Models.SortDirection.Ascending);

        var storage = sort.ToStorageField();

        storage.Field.ShouldBe(TestSortField.CreatedAt);
        storage.Direction.ShouldBe(Duende.Storage.Internal.Querying.SortDirection.Ascending);
    }

    // === QueryResult: paging fields ===

    [Fact]
    public void QueryResult_maps_items_and_all_paging_fields()
    {
        var storageResult = new Duende.Storage.Internal.Querying.QueryResult<string>
        {
            Items = ["a", "b"],
            NextToken = (StorageContinuationToken)"next",
            PreviousToken = (StorageContinuationToken)"prev",
            HasMoreData = true,
            HasPreviousData = true,
            TotalCount = 2,
            TotalPages = 1
        };

        var range = Models.DataRange.FromContinuationToken((Models.ContinuationToken)"some-token");
        var model = storageResult.ToModel(range);

        model.Results.ShouldBe(["a", "b"]);
        model.ResultsToken.ShouldBe("next");
        model.PreviousToken.ShouldBe("prev");
        model.HasNextResults.ShouldBeTrue();
        model.HasPrevResults.ShouldBeTrue();
        model.TotalCount.ShouldBe(2);
        model.TotalPages.ShouldBe(1);
    }

    [Fact]
    public void QueryResult_with_no_tokens_maps_to_a_null_previous_token_and_no_previous_results()
    {
        var storageResult = new Duende.Storage.Internal.Querying.QueryResult<string>
        {
            Items = [],
            HasMoreData = false,
            HasPreviousData = false
        };

        var model = storageResult.ToModel(Models.DataRange.FromOffset(0, 10));

        model.PreviousToken.ShouldBeNull();
        model.HasPrevResults.ShouldBeFalse();
    }

    [Fact]
    public void QueryResult_HasPrevResults_passes_through_storage_HasPreviousData_when_true()
    {
        var storageResult = new Duende.Storage.Internal.Querying.QueryResult<string>
        {
            Items = ["a"],
            PreviousToken = (StorageContinuationToken)"prev",
            HasMoreData = true,
            HasPreviousData = true
        };

        var range = Models.DataRange.FromContinuationToken((Models.ContinuationToken)"abc");
        var model = storageResult.ToModel(range);

        model.HasPrevResults.ShouldBeTrue();
    }

    [Fact]
    public void QueryResult_HasPrevResults_passes_through_storage_HasPreviousData_when_false()
    {
        var storageResult = new Duende.Storage.Internal.Querying.QueryResult<string>
        {
            Items = ["a"],
            PreviousToken = (StorageContinuationToken)"prev",
            HasMoreData = true,
            HasPreviousData = false
        };

        var range = Models.DataRange.FromContinuationToken((Models.ContinuationToken?)null);
        var model = storageResult.ToModel(range);

        model.HasPrevResults.ShouldBeFalse();
    }

    [Fact]
    public void QueryResult_null_range_passes_through_storage_HasPreviousData()
    {
        var storageResult = new Duende.Storage.Internal.Querying.QueryResult<string>
        {
            Items = ["a"],
            PreviousToken = (StorageContinuationToken)"prev",
            HasPreviousData = true
        };

        var model = storageResult.ToModel(null);

        model.HasPrevResults.ShouldBeTrue();
    }

    // === SaveResult / GetResult / StorageError ===

    [Fact]
    public void SaveResult_success_maps_id_and_version()
    {
        var storageResult = StorageTestResults.Success<string>("id-1", 3);

        var model = storageResult.ToModel();

        model.IsSuccess.ShouldBeTrue();
        model.Id.ShouldBe("id-1");
        model.Version.ShouldNotBeNull();
        model.Version!.Value.ShouldBe(3);
        model.Errors.ShouldBeNull();
    }

    [Fact]
    public void SaveResult_failure_maps_errors()
    {
        var storageResult = StorageTestResults.Failure<string>(Duende.Storage.Internal.ErrorResult.NotFound("Client", "abc"));

        var model = storageResult.ToModel();

        model.IsSuccess.ShouldBeFalse();
        model.Errors.ShouldNotBeNull();
        model.Errors!.Count.ShouldBe(1);
        model.Errors[0].Code.ShouldBe("not_found");
    }

    [Fact]
    public void GetResult_found_maps_item_and_version()
    {
        var storageResult = StorageTestResults.Found("item-value", 7);

        var model = storageResult.ToModel();

        model.Found.ShouldBeTrue();
        model.Item.ShouldBe("item-value");
        model.Version.ShouldNotBeNull();
        model.Version!.Value.ShouldBe(7);
    }

    [Fact]
    public void GetResult_not_found_maps_to_no_item_or_version()
    {
        var storageResult = StorageTestResults.NotFound<string>();

        var model = storageResult.ToModel();

        model.Found.ShouldBeFalse();
        model.Item.ShouldBeNull();
        model.Version.ShouldBeNull();
    }

    [Fact]
    public void StorageError_maps_code_message_and_property_names()
    {
        var storageError = Duende.Storage.Internal.ErrorResult.DuplicateValue("clientId", "abc");

        var model = storageError.ToModel();

        model.Code.ShouldBe("duplicate_value");
        model.Message.ShouldBe(storageError.Message);
        model.PropertyNames.ShouldBe(["clientId"]);
    }
}
