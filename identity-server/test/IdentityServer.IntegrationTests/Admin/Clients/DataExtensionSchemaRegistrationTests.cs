// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.Clients;

/// <summary>
/// Integration tests verifying the <c>AddInMemoryDataExtensionSchemas</c> and
/// <c>AddDynamicSchemas</c> extension methods for configuring
/// data extension schema services.
/// </summary>
public sealed class DataExtensionSchemaRegistrationTests(WebServerFixture webApp) : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task in_memory_schemas_allow_extended_properties_round_trip()
    {
        await using var server = await CreateServer(builder =>
            builder.AddInMemoryDataExtensionSchemas([TestClientAttributes.Schema]));

        var admin = server.GetRequiredService<IClientAdmin>();

        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        client.ExtendedProperties.Set(TestClientAttributes.Department, "Engineering");
        client.ExtendedProperties.Set(TestClientAttributes.CostCenter, 42);

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var dept = getResult.Item.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.Department.Code);
        dept.ShouldNotBeNull();
        dept.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("Engineering");

        var cc = getResult.Item.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.CostCenter.Code);
        cc.ShouldNotBeNull();
        cc.ShouldBeOfType<AttributeValue<int>>().TypedValue.ShouldBe(42);
    }

    [Fact]
    public async Task in_memory_schemas_do_not_register_schema_admin()
    {
        await using var server = await CreateServer(builder =>
            builder.AddInMemoryDataExtensionSchemas([TestClientAttributes.Schema]));

        server.Services.GetService<ISchemaAdmin>().ShouldBeNull();
    }

    [Fact]
    public async Task when_the_same_schema_id_is_registered_twice_only_the_later_schema_applies()
    {
        await using var server = await CreateServer(builder => builder
            .AddInMemoryDataExtensionSchemas([new SchemaConfiguration
            {
                SchemaId = SchemaId.Client,
                AttributeDefinitions = [TestClientAttributes.Department]
            }])
            .AddInMemoryDataExtensionSchemas([new SchemaConfiguration
            {
                SchemaId = SchemaId.Client,
                AttributeDefinitions = [TestClientAttributes.CostCenter]
            }]));

        var admin = server.GetRequiredService<IClientAdmin>();
        var laterSchemaClient = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        laterSchemaClient.ExtendedProperties.Set(TestClientAttributes.CostCenter, 7);
        var earlierSchemaClient = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        earlierSchemaClient.ExtendedProperties.Set(TestClientAttributes.Department, "Engineering");

        var laterSchemaResult = await admin.CreateAsync(laterSchemaClient, _ct);
        var earlierSchemaResult = await admin.CreateAsync(earlierSchemaClient, _ct);

        laterSchemaResult.IsSuccess.ShouldBeTrue($"Create failed: {laterSchemaResult}");
        earlierSchemaResult.IsSuccess.ShouldBeFalse();
        earlierSchemaResult.Errors.ShouldNotBeNull();
        earlierSchemaResult.Errors.ShouldContain(e => e.Code == "validation_failed" && e.Message.Contains("department"));
    }

    [Fact]
    public async Task in_memory_schemas_are_not_used_when_schemas_are_stored_in_the_database()
    {
        await using var server = await CreateServer(builder => builder
            .AddInMemoryDataExtensionSchemas([TestClientAttributes.Schema])
            .AddDynamicSchemas());

        var admin = server.GetRequiredService<IClientAdmin>();
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        client.ExtendedProperties.Set(TestClientAttributes.Department, "Engineering");

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed" && e.Message.Contains("department"));
    }

    [Fact]
    public async Task storage_schemas_allow_extended_properties_round_trip()
    {
        await using var server = await CreateServer(_ => { _.Services.AddDynamicSchemaStorage(); });

        var schemaAdmin = server.GetRequiredService<ISchemaAdmin>();
        var createSchemaResult = await schemaAdmin.CreateAsync(TestClientAttributes.Schema, _ct);
        createSchemaResult.IsSuccess.ShouldBeTrue();

        var admin = server.GetRequiredService<IClientAdmin>();

        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        client.ExtendedProperties.Set(TestClientAttributes.Department, "Finance");
        client.ExtendedProperties.Set(TestClientAttributes.CostCenter, 99);

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var dept = getResult.Item.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.Department.Code);
        dept.ShouldNotBeNull();
        dept.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("Finance");

        var cc = getResult.Item.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.CostCenter.Code);
        cc.ShouldNotBeNull();
        cc.ShouldBeOfType<AttributeValue<int>>().TypedValue.ShouldBe(99);
    }

    [Fact]
    public async Task storage_schemas_registers_schema_admin()
    {
        await using var server = await CreateServer(sp => { sp.AddDynamicSchemas(); });

        var schemaAdmin = server.Services.GetService<ISchemaAdmin>();
        _ = schemaAdmin.ShouldNotBeNull();
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<KestrelBasedTestServer> CreateServer(Action<IIdentityServerBuilder> configureSchemas)
    {
        var output = TestContext.Current.TestOutputHelper!;
        var dbName = $"schema_reg_{Guid.NewGuid():N}";

        var server = new KestrelBasedTestServer(
            "schema-reg",
            webApp,
            new PrefixedTestOutputHelper(output, "schema-reg"),
            services =>
            {
                services.AddRouting();

                var isBuilder = services.AddIdentityServer()
                    .AddStorage(storage =>
                        storage.AddSqlite(opt =>
                            opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"))
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddClientConfigurationValidator<Validation.NopClientConfigurationValidator>();

                configureSchemas(isBuilder);
            },
            _ => { });

        await server.StartAsync();

        var schema = server.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        return server;
    }
}
