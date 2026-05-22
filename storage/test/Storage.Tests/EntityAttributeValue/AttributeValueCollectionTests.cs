// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable CS0618 // Type or member is obsolete
using Duende.Storage.EntityAttributeValue.Internal;

namespace Duende.Storage.EntityAttributeValue;

public static class AttributeValueCollectionTests
{
    private static readonly AttributeDescription Desc = AttributeDescription.Create("test");

    private static AttributeSchema SchemaWith(params AttributeDefinition[] definitions)
    {
        var schema = AttributeSchema.Load([], []);
        foreach (var def in definitions)
        {
            _ = schema.AddAttributeDefinition(def);
        }
        return schema;
    }

    private static AttributeDefinition StringDef(string name) =>
        new() { Code = AttributeCode.Create(name), AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = Desc };

    // --- Set ---

    [Fact]
    public static void set_adds_new_attribute()
    {
        var schema = SchemaWith(StringDef("color"));
        var collection = new AttributeValueCollection(schema);

        collection.Set(AttributeCode.Create("color"), "red");

        collection.Count.ShouldBe(1);
    }

    [Fact]
    public static void set_replaces_existing_attribute()
    {
        var schema = SchemaWith(StringDef("color"));
        var name = AttributeCode.Create("color");
        var collection = new AttributeValueCollection(schema);

        collection.Set(name, "red");
        collection.Set(name, "blue");

        collection.Count.ShouldBe(1);
        collection[name].UntypedValue.ShouldBe("blue");
    }

    [Fact]
    public static void set_throws_for_null()
    {
        var collection = new AttributeValueCollection(SchemaWith());

        var ex = Record.Exception(() => collection.Set(null!));

        _ = ex.ShouldBeOfType<ArgumentNullException>();
    }

    // --- Remove ---

    [Fact]
    public static void remove_returns_true_when_present()
    {
        var schema = SchemaWith(StringDef("color"));
        var name = AttributeCode.Create("color");
        var collection = new AttributeValueCollection(schema);
        collection.Set(name, "red");

        var result = collection.Remove(name);

        result.ShouldBeTrue();
        collection.Count.ShouldBe(0);
    }

    [Fact]
    public static void remove_returns_false_when_absent()
    {
        var collection = new AttributeValueCollection(SchemaWith());

        var result = collection.Remove(AttributeCode.Create("missing"));

        result.ShouldBeFalse();
    }

    // --- Contains ---

    [Fact]
    public static void contains_returns_true_when_present()
    {
        var schema = SchemaWith(StringDef("color"));
        var name = AttributeCode.Create("color");
        var collection = new AttributeValueCollection(schema);
        collection.Set(name, "red");

        collection.Contains(name).ShouldBeTrue();
    }

    [Fact]
    public static void contains_returns_false_when_absent()
    {
        var collection = new AttributeValueCollection(SchemaWith());

        collection.Contains(AttributeCode.Create("missing")).ShouldBeFalse();
    }

    // --- TryGet ---

    [Fact]
    public static void try_get_returns_true_and_value_when_present()
    {
        var schema = SchemaWith(StringDef("color"));
        var name = AttributeCode.Create("color");
        var collection = new AttributeValueCollection(schema);
        collection.Set(name, "red");

        var found = collection.TryGet(name, out var attribute);

        found.ShouldBeTrue();
        _ = attribute.ShouldNotBeNull();
        attribute.UntypedValue.ShouldBe("red");
    }

    [Fact]
    public static void try_get_returns_false_when_absent()
    {
        var collection = new AttributeValueCollection(SchemaWith());

        var found = collection.TryGet(AttributeCode.Create("missing"), out var attribute);

        found.ShouldBeFalse();
        attribute.ShouldBeNull();
    }

    // --- Indexer ---

    [Fact]
    public static void indexer_returns_attribute_when_present()
    {
        var schema = SchemaWith(StringDef("color"));
        var name = AttributeCode.Create("color");
        var collection = new AttributeValueCollection(schema);
        collection.Set(name, "red");

        var attribute = collection[name];

        attribute.UntypedValue.ShouldBe("red");
    }

    [Fact]
    public static void indexer_throws_when_absent()
    {
        var collection = new AttributeValueCollection(SchemaWith());

        var ex = Record.Exception(() => _ = collection[AttributeCode.Create("missing")]);

        _ = ex.ShouldBeOfType<KeyNotFoundException>();
    }

    // --- Count ---

    [Fact]
    public static void empty_collection_has_zero_count()
    {
        var collection = new AttributeValueCollection(SchemaWith());

        collection.Count.ShouldBe(0);
    }

    // --- GetEnumerator ---

    [Fact]
    public static void enumerator_yields_all_attributes()
    {
        var schema = SchemaWith(StringDef("color"), StringDef("size"));
        var collection = new AttributeValueCollection(schema);
        collection.Set(AttributeCode.Create("color"), "red");
        collection.Set(AttributeCode.Create("size"), "large");

        var items = collection.ToList();

        items.Count.ShouldBe(2);
    }

    // --- Constructor duplicate rejection ---

    [Fact]
    public static void constructor_rejects_duplicate_names()
    {
        var schema = SchemaWith(StringDef("color"));
        var name = AttributeCode.Create("color");
        var attr1 = new AttributeValue<string>(name, "red");
        var attr2 = new AttributeValue<string>(name, "blue");

        var ex = Record.Exception(() => new AttributeValueCollection(schema, [attr1, attr2]));

        _ = ex.ShouldBeOfType<ArgumentException>();
        ex.Message.ShouldContain("color");
    }

    [Fact]
    public static void constructor_rejects_duplicate_names_different_casing()
    {
        var schema = SchemaWith(StringDef("color"));
        var attr1 = new AttributeValue<string>(AttributeCode.Create("color"), "red");
        var attr2 = new AttributeValue<string>(AttributeCode.Create("Color"), "blue");

        var ex = Record.Exception(() => new AttributeValueCollection(schema, [attr1, attr2]));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }

    // --- Case-insensitive lookups ---

    [Fact]
    public static void try_get_finds_attribute_with_different_casing()
    {
        var schema = SchemaWith(StringDef("givenName"));
        var collection = new AttributeValueCollection(schema);
        collection.Set(AttributeCode.Create("givenName"), "Alice");

        collection.TryGet(AttributeCode.Create("givenname"), out var attr).ShouldBeTrue();
        _ = attr.ShouldNotBeNull();
        attr.UntypedValue.ShouldBe("Alice");
    }

    [Fact]
    public static void contains_matches_case_insensitively()
    {
        var schema = SchemaWith(StringDef("givenName"));
        var collection = new AttributeValueCollection(schema);
        collection.Set(AttributeCode.Create("givenName"), "Alice");

        collection.Contains(AttributeCode.Create("GIVENNAME")).ShouldBeTrue();
        collection.Contains(AttributeCode.Create("givenname")).ShouldBeTrue();
        collection.Contains(AttributeCode.Create("GivenName")).ShouldBeTrue();
    }

    [Fact]
    public static void set_replaces_attribute_with_different_casing()
    {
        var schema = SchemaWith(StringDef("givenName"));
        var collection = new AttributeValueCollection(schema);
        collection.Set(AttributeCode.Create("givenName"), "Alice");
        collection.Set(AttributeCode.Create("GIVENNAME"), "Bob");

        collection.Count.ShouldBe(1);
        collection.TryGet(AttributeCode.Create("givenName"), out var attr).ShouldBeTrue();
        attr!.UntypedValue.ShouldBe("Bob");
    }

    [Fact]
    public static void remove_works_with_different_casing()
    {
        var schema = SchemaWith(StringDef("givenName"));
        var collection = new AttributeValueCollection(schema);
        collection.Set(AttributeCode.Create("givenName"), "Alice");

        collection.Remove(AttributeCode.Create("GIVENNAME")).ShouldBeTrue();
        collection.Count.ShouldBe(0);
    }

    [Fact]
    public static void stored_attribute_preserves_original_casing()
    {
        var schema = SchemaWith(StringDef("givenName"));
        var collection = new AttributeValueCollection(schema);
        collection.Set(AttributeCode.Create("givenName"), "Alice");

        var attr = collection[AttributeCode.Create("givenname")];

        attr.Code.Value.ShouldBe("givenName", "Original casing should be preserved in storage");
    }
}
