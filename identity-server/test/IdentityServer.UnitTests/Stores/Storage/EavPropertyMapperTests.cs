// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.EntityAttributeValue.Internal.Storage;

namespace UnitTests.Stores.Storage;

public class EavPropertyMapperTests
{
    [Fact]
    public void extract_null_entries_returns_empty_dictionary()
    {
        var result = EavPropertyMapper.ExtractStringProperties(null);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void extract_empty_entries_returns_empty_dictionary()
    {
        var result = EavPropertyMapper.ExtractStringProperties([]);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void extract_only_string_typed_entries()
    {
        var entries = new List<AttributeValueDso.V1>
        {
            new("name", "my-api"),
            new("version", 2),
            new("active", true)
        };

        var result = EavPropertyMapper.ExtractStringProperties(entries);

        result.Count.ShouldBe(1);
        result["name"].ShouldBe("my-api");
    }

    [Fact]
    public void extract_multiple_string_entries()
    {
        var entries = new List<AttributeValueDso.V1>
        {
            new("name", "my-api"),
            new("description", "A test API"),
            new("cost", 9.99m)
        };

        var result = EavPropertyMapper.ExtractStringProperties(entries);

        result.Count.ShouldBe(2);
        result["name"].ShouldBe("my-api");
        result["description"].ShouldBe("A test API");
    }

    [Fact]
    public void extract_skips_null_values()
    {
        var entries = new List<AttributeValueDso.V1>
        {
            new("name", null),
            new("dept", "Engineering")
        };

        var result = EavPropertyMapper.ExtractStringProperties(entries);

        result.Count.ShouldBe(1);
        result["dept"].ShouldBe("Engineering");
    }

    [Fact]
    public void extract_handles_json_element_string()
    {
        var json = JsonSerializer.SerializeToElement("hello");
        var entries = new List<AttributeValueDso.V1>
        {
            new("greeting", json)
        };

        var result = EavPropertyMapper.ExtractStringProperties(entries);

        result.Count.ShouldBe(1);
        result["greeting"].ShouldBe("hello");
    }

    [Fact]
    public void extract_skips_json_element_number()
    {
        var json = JsonSerializer.SerializeToElement(42);
        var entries = new List<AttributeValueDso.V1>
        {
            new("count", json),
            new("name", "test")
        };

        var result = EavPropertyMapper.ExtractStringProperties(entries);

        result.Count.ShouldBe(1);
        result["name"].ShouldBe("test");
    }
}
