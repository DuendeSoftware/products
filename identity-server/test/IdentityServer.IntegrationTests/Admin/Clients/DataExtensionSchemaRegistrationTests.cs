// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.Clients;

/// <summary>
/// Integration tests verifying the <c>AddInMemoryDataExtensionSchemas</c> and
/// <c>AddStorageDataExtensionSchemas</c> extension methods for configuring
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

        var schemaAdmin = server.Services.GetService<ISchemaAdmin>();
        schemaAdmin.ShouldBeNull();
    }

    [Fact]
    public async Task storage_schemas_allow_extended_properties_round_trip()
    {
        await using var server = await CreateServer(builder =>
            builder.AddStorageDataExtensionSchemas());

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
        await using var server = await CreateServer(builder =>
            builder.AddStorageDataExtensionSchemas());

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

                services.AddStorageInternal(storage =>
                    storage.AddSqliteStore(opt =>
                        opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

                var isBuilder = services.AddIdentityServer()
                    .AddConfigurationStorage()
                    .AddClientConfigurationValidator<Validation.NopClientConfigurationValidator>();

                configureSchemas(isBuilder);
            },
            _ => { });

        await server.StartAsync();

        var schema = server.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        return server;
    }
}
