// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class InMemorySchemaStoreTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static SchemaConfiguration MakeConfig(string schemaId, params string[] attributeCodes) =>
        new()
        {
            SchemaId = SchemaId.Create(schemaId),
            DisplayName = schemaId,
            AttributeDefinitions = attributeCodes
                .Select(c => new AttributeDefinition
                {
                    Code = AttributeCode.Create(c),
                    AttributeType = new ScalarAttributeType(ScalarDataType.String)
                })
                .ToList<AttributeDefinition>()
        };

    [Fact]
    public static async Task get_returns_schema_for_known_id()
    {
        var config = MakeConfig("client", "department");
        var store = new InMemorySchemaStore([config]);

        var schema = await store.GetAsync(SchemaId.Create("client"), Ct);

        _ = schema.ShouldNotBeNull();
    }

    [Fact]
    public static async Task get_returns_null_for_unknown_id()
    {
        var config = MakeConfig("client", "department");
        var store = new InMemorySchemaStore([config]);

        var schema = await store.GetAsync(SchemaId.Create("unknown"), Ct);

        schema.ShouldBeNull();
    }

    [Fact]
    public static async Task get_is_case_insensitive()
    {
        var config = MakeConfig("client", "department");
        var store = new InMemorySchemaStore([config]);

        var schema = await store.GetAsync(SchemaId.Create("CLIENT"), Ct);

        _ = schema.ShouldNotBeNull();
    }

    [Fact]
    public static async Task get_returns_schema_with_correct_attribute_definitions()
    {
        var config = MakeConfig("client", "department", "environment");
        var store = new InMemorySchemaStore([config]);

        var schema = await store.GetAsync(SchemaId.Create("client"), Ct);

        _ = schema.ShouldNotBeNull();
        schema.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("department"));
        schema.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("environment"));
    }

    [Fact]
    public static async Task get_returns_correct_schema_when_multiple_registered()
    {
        var clientConfig = MakeConfig("client", "department");
        var idpConfig = MakeConfig("idp", "provider_type");
        var store = new InMemorySchemaStore([clientConfig, idpConfig]);

        var clientSchema = await store.GetAsync(SchemaId.Create("client"), Ct);

        _ = clientSchema.ShouldNotBeNull();
        clientSchema.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("department"));
        clientSchema.AttributeDefinitions.ShouldNotContainKey(AttributeCode.Create("provider_type"));
    }

    [Fact]
    public static async Task get_returns_null_when_no_schemas_registered()
    {
        var store = new InMemorySchemaStore([]);

        var schema = await store.GetAsync(SchemaId.Create("client"), Ct);

        schema.ShouldBeNull();
    }
}
