// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.Clients;

/// <summary>
/// Integration tests verifying that <see cref="ClientConfiguration.ExtendedProperties"/>
/// are validated against the client schema and round-trip correctly through the admin store.
/// </summary>
public sealed class ClientExtendedPropertiesTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task create_with_unknown_attribute_returns_validation_error()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        client.ExtendedProperties.Set(AttributeCode.Create("unknown_attribute"), "value");

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task extended_properties_round_trip_after_create()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        client.ExtendedProperties.Set(TestClientAttributes.Department, "Engineering");
        client.ExtendedProperties.Set(TestClientAttributes.CostCenter, 1042);

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.ExtendedProperties.Count.ShouldBe(2);

        var deptAttr = loaded.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.Department.Code);
        deptAttr.ShouldNotBeNull();
        deptAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("Engineering");

        var ccAttr = loaded.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.CostCenter.Code);
        ccAttr.ShouldNotBeNull();
        ccAttr.ShouldBeOfType<AttributeValue<int>>().TypedValue.ShouldBe(1042);
    }

    [Fact]
    public async Task no_extended_properties_round_trips_as_empty_collection()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item.ExtendedProperties.Count.ShouldBe(0);
    }

    [Fact]
    public async Task update_with_extended_properties_succeeds()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ExtendedProperties.Set(TestClientAttributes.Department, "Finance");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        var deptAttr = afterUpdate.Item.ExtendedProperties.FirstOrDefault(x => x.Code == TestClientAttributes.Department.Code);
        deptAttr.ShouldNotBeNull();
        deptAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("Finance");
    }

    [Fact]
    public async Task update_with_unknown_attribute_returns_validation_error()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ExtendedProperties.Set(AttributeCode.Create("bad_attr"), "value");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task update_can_clear_extended_properties()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        client.ExtendedProperties.Set(TestClientAttributes.Department, "Engineering");

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ExtendedProperties.Remove(TestClientAttributes.Department.Code);

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update (clear) failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.ExtendedProperties.Count.ShouldBe(0);
    }

    [Fact]
    public async Task create_with_extended_properties_fails_when_no_schema_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var dbName = $"test_{Guid.NewGuid():N}";
        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        services.AddIdentityServer()
            .AddConfigurationStorage();

        services.AddSingleton<ISchemaStore>(
            new InMemorySchemaStore([]));

        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<Duende.Storage.Schema.IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();

        var client = new CreateClient { ClientId = $"client_{Guid.NewGuid():N}" };
        client.ExtendedProperties.Set(TestClientAttributes.Department, "Engineering");

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
