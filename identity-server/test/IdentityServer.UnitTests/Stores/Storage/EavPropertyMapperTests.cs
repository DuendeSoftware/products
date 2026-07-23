// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.EntityAttributeValue;

namespace UnitTests.Stores.Storage;

public class EavPropertyMapperTests
{
    // DeserializeToCollection

    [Fact]
    public void deserialize_null_entries_returns_empty_collection()
    {
        var result = EavPropertyMapper.DeserializeToCollection(null);

        result.Count.ShouldBe(0);
    }

    [Fact]
    public void deserialize_string_entry_round_trips()
    {
        var entries = new[] { new AttributeValueEntryDso("dept", "string", "Engineering") };

        var result = EavPropertyMapper.DeserializeToCollection(entries);

        result.TryGet(AttributeCode.Create("dept"), out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("Engineering");
    }

    [Fact]
    public void deserialize_integer_entry_round_trips()
    {
        var entries = new[] { new AttributeValueEntryDso("cost_center", "integer", "1042") };

        var result = EavPropertyMapper.DeserializeToCollection(entries);

        result.TryGet(AttributeCode.Create("cost_center"), out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<int>>().TypedValue.ShouldBe(1042);
    }

    [Fact]
    public void deserialize_boolean_entry_round_trips()
    {
        var entries = new[] { new AttributeValueEntryDso("is_active", "boolean", "true") };

        var result = EavPropertyMapper.DeserializeToCollection(entries);

        result.TryGet(AttributeCode.Create("is_active"), out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<bool>>().TypedValue.ShouldBeTrue();
    }

    [Fact]
    public void deserialize_decimal_entry_round_trips()
    {
        var entries = new[] { new AttributeValueEntryDso("rate", "decimal", "3.14") };

        var result = EavPropertyMapper.DeserializeToCollection(entries);

        result.TryGet(AttributeCode.Create("rate"), out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<decimal>>().TypedValue.ShouldBe(3.14m);
    }

    [Fact]
    public void deserialize_date_entry_round_trips()
    {
        var date = new DateOnly(2025, 6, 15);
        var entries = new[] { new AttributeValueEntryDso("start_date", "date", date.ToString("O")) };

        var result = EavPropertyMapper.DeserializeToCollection(entries);

        result.TryGet(AttributeCode.Create("start_date"), out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<DateOnly>>().TypedValue.ShouldBe(date);
    }

    [Fact]
    public void deserialize_datetime_entry_round_trips()
    {
        var dto = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var entries = new[] { new AttributeValueEntryDso("created_at", "datetime", dto.ToString("O")) };

        var result = EavPropertyMapper.DeserializeToCollection(entries);

        result.TryGet(AttributeCode.Create("created_at"), out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<DateTimeOffset>>().TypedValue.ShouldBe(dto);
    }

    [Fact]
    public void deserialize_unknown_data_type_throws()
    {
        var entries = new[] { new AttributeValueEntryDso("x", "unknown_type", "value") };

        Should.Throw<InvalidOperationException>(() =>
            EavPropertyMapper.DeserializeToCollection(entries));
    }

    // SerializeFromCollection

    [Fact]
    public void serialize_empty_collection_returns_empty_list()
    {
        var collection = new AttributeValueCollection();

        var result = EavPropertyMapper.SerializeFromCollection(collection);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void serialize_string_value_produces_correct_entry()
    {
        var collection = new AttributeValueCollection();
        collection.Set(AttributeCode.Create("dept"), "Engineering");

        var result = EavPropertyMapper.SerializeFromCollection(collection);

        result.ShouldHaveSingleItem().ShouldBe(new AttributeValueEntryDso("dept", "string", "Engineering"));
    }

    // Full round-trip

    [Fact]
    public void full_round_trip_preserves_all_data_types()
    {
        var original = new AttributeValueCollection();
        original.Set(AttributeCode.Create("str"), "hello");
        original.Set(AttributeCode.Create("num"), 42);
        original.Set(AttributeCode.Create("flag"), false);
        original.Set(AttributeCode.Create("amount"), 9.99m);

        var serialized = EavPropertyMapper.SerializeFromCollection(original);
        var restored = EavPropertyMapper.DeserializeToCollection(serialized);

        restored.Count.ShouldBe(4);
        restored.TryGet(AttributeCode.Create("str"), out var s).ShouldBeTrue();
        s.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("hello");

        restored.TryGet(AttributeCode.Create("num"), out var n).ShouldBeTrue();
        n.ShouldBeOfType<AttributeValue<int>>().TypedValue.ShouldBe(42);

        restored.TryGet(AttributeCode.Create("flag"), out var f).ShouldBeTrue();
        f.ShouldBeOfType<AttributeValue<bool>>().TypedValue.ShouldBeFalse();

        restored.TryGet(AttributeCode.Create("amount"), out var a).ShouldBeTrue();
        a.ShouldBeOfType<AttributeValue<decimal>>().TypedValue.ShouldBe(9.99m);
    }

    // ExtractStringProperties

    [Fact]
    public void extract_null_entries_returns_empty_dictionary()
    {
        var result = EavPropertyMapper.ExtractStringProperties(null);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void extract_only_string_typed_entries()
    {
        var entries = new[]
        {
            new AttributeValueEntryDso("name", "string", "my-api"),
            new AttributeValueEntryDso("version", "integer", "2"),
            new AttributeValueEntryDso("active", "boolean", "true")
        };

        var result = EavPropertyMapper.ExtractStringProperties(entries);

        result.Count.ShouldBe(1);
        result["name"].ShouldBe("my-api");
    }
}
