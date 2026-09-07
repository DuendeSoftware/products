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
        var storage = new InMemorySchemaStore([config]);

        var result = await storage.GetAsync(SchemaId.Create("client"), Ct);

        _ = result.ShouldNotBeNull();
        result.AttributeDefinitions.ShouldNotBeEmpty();
    }

    [Fact]
    public static async Task get_returns_empty_schema_for_unknown_id()
    {
        var config = MakeConfig("client", "department");
        var storage = new InMemorySchemaStore([config]);

        var result = await storage.GetAsync(SchemaId.Create("unknown"), Ct);

        _ = result.ShouldNotBeNull();
        result.AttributeDefinitions.ShouldBeEmpty();
        result.Groups.ShouldBeEmpty();
    }

    [Fact]
    public static async Task get_is_case_insensitive()
    {
        var config = MakeConfig("client", "department");
        var storage = new InMemorySchemaStore([config]);

        var result = await storage.GetAsync(SchemaId.Create("CLIENT"), Ct);

        result.AttributeDefinitions.ShouldNotBeEmpty();
    }

    [Fact]
    public static async Task get_returns_schema_with_correct_attribute_definitions()
    {
        var config = MakeConfig("client", "department", "environment");
        var storage = new InMemorySchemaStore([config]);

        var result = await storage.GetAsync(SchemaId.Create("client"), Ct);

        _ = result.ShouldNotBeNull();
        result.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("department"));
        result.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("environment"));
    }

    [Fact]
    public static async Task get_returns_correct_schema_when_multiple_registered()
    {
        var clientConfig = MakeConfig("client", "department");
        var idpConfig = MakeConfig("idp", "provider_type");
        var storage = new InMemorySchemaStore([clientConfig, idpConfig]);

        var result = await storage.GetAsync(SchemaId.Create("client"), Ct);

        _ = result.ShouldNotBeNull();
        result.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("department"));
        result.AttributeDefinitions.ShouldNotContainKey(AttributeCode.Create("provider_type"));
    }

    [Fact]
    public static async Task get_returns_empty_schema_when_no_schemas_registered()
    {
        var storage = new InMemorySchemaStore([]);

        var result = await storage.GetAsync(SchemaId.Create("client"), Ct);

        result.AttributeDefinitions.ShouldBeEmpty();
    }
}
