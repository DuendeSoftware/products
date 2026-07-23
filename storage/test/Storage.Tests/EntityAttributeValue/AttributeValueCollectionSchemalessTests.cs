// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
/// Tests for <see cref="AttributeValueCollection"/> created via the parameterless
/// constructor (schema-less mode). Validation is deferred to the admin store boundary.
/// </summary>
public static class AttributeValueCollectionSchemalessTests
{
    [Fact]
    public static void parameterless_ctor_creates_empty_collection()
    {
        var collection = new AttributeValueCollection();
        collection.ShouldBeEmpty();
    }

    [Fact]
    public static void set_string_succeeds_without_schema()
    {
        var collection = new AttributeValueCollection();
        collection.Set(AttributeCode.Create("color"), "red");
        collection.Count.ShouldBe(1);
    }

    [Fact]
    public static void set_replaces_existing_attribute_without_schema()
    {
        var collection = new AttributeValueCollection();
        var code = AttributeCode.Create("color");
        collection.Set(code, "red");
        collection.Set(code, "blue");
        collection.Count.ShouldBe(1);
        collection[code].UntypedValue.ShouldBe("blue");
    }

    [Fact]
    public static void validate_throws_invalid_operation_when_no_schema()
    {
        var collection = new AttributeValueCollection();
        collection.Set(AttributeCode.Create("x"), "value");

        _ = Should.Throw<InvalidOperationException>(() => collection.Validate());
    }

    [Fact]
    public static void try_validate_returns_false_when_no_schema()
    {
        var collection = new AttributeValueCollection();
        collection.Set(AttributeCode.Create("x"), "value");

        var succeeded = collection.TryValidate(out _, out var errors);

        succeeded.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public static void try_validate_against_succeeds_for_valid_attributes()
    {
        var code = AttributeCode.Create("department");
        var schema = BuildSchema(code, ScalarDataType.String);
        var collection = new AttributeValueCollection();
        collection.Set(code, "Engineering");

        var succeeded = collection.TryValidateAgainst(schema, out var errors);

        succeeded.ShouldBeTrue();
        errors.ShouldBeNull();
    }

    [Fact]
    public static void try_validate_against_fails_for_unknown_attribute()
    {
        var schema = BuildSchema(AttributeCode.Create("department"), ScalarDataType.String);
        var collection = new AttributeValueCollection();
        collection.Set(AttributeCode.Create("unknown_code"), "value");

        var succeeded = collection.TryValidateAgainst(schema, out var errors);

        succeeded.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("unknown_code"));
    }

    [Fact]
    public static void try_validate_against_fails_for_wrong_type()
    {
        var code = AttributeCode.Create("count");
        var schema = BuildSchema(code, ScalarDataType.Integer);
        var collection = new AttributeValueCollection();
        collection.Set(code, "not-an-int");

        var succeeded = collection.TryValidateAgainst(schema, out var errors);

        succeeded.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
    }

    [Fact]
    public static void try_validate_against_throws_for_null_schema()
    {
        var collection = new AttributeValueCollection();
        _ = Should.Throw<ArgumentNullException>(() => collection.TryValidateAgainst(null!, out _));
    }

    [Fact]
    public static void try_validate_against_empty_collection_succeeds()
    {
        var schema = BuildSchema(AttributeCode.Create("optional_field"), ScalarDataType.String);
        var collection = new AttributeValueCollection();

        var succeeded = collection.TryValidateAgainst(schema, out var errors);

        succeeded.ShouldBeTrue();
        errors.ShouldBeNull();
    }

    private static IReadOnlyAttributeSchema BuildSchema(AttributeCode code, ScalarDataType dataType)
    {
        var config = new SchemaConfiguration
        {
            SchemaId = SchemaId.Create("test"),
            AttributeDefinitions =
            [
                new AttributeDefinition
                {
                    Code = code,
                    AttributeType = new ScalarAttributeType(dataType)
                }
            ]
        };

        var store = new InMemorySchemaStore([config]);
        return store.GetAsync(config.SchemaId, CancellationToken.None)
            .GetAwaiter().GetResult()!;
    }
}
