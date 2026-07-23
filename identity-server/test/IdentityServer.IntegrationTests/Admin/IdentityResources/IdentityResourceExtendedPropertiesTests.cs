// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.IdentityResources;

/// <summary>
/// Integration tests verifying that <see cref="IdentityResourceConfiguration.ExtendedProperties"/>
/// are validated against the identity resource schema and round-trip correctly through the admin store.
/// </summary>
public sealed class IdentityResourceExtendedPropertiesTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task create_with_unknown_attribute_returns_validation_error()
    {
        var admin = _fixture.IdentityResourceAdmin;
        var resource = new IdentityResourceConfiguration { Name = $"identity_{Guid.NewGuid():N}" };
        resource.ExtendedProperties.Set(AttributeCode.Create("unknown_attr"), "value");

        var result = await admin.CreateAsync(resource, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task extended_properties_round_trip_after_create()
    {
        var admin = _fixture.IdentityResourceAdmin;
        var resource = new IdentityResourceConfiguration { Name = $"identity_{Guid.NewGuid():N}" };
        resource.ExtendedProperties.Set(TestIdentityResourceAttributes.Owner, "platform-team");
        resource.ExtendedProperties.Set(TestIdentityResourceAttributes.Version, 2);

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.ExtendedProperties.Count.ShouldBe(2);

        loaded.ExtendedProperties.TryGet(TestIdentityResourceAttributes.Owner.Code, out var ownerAttr).ShouldBeTrue();
        ownerAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("platform-team");

        loaded.ExtendedProperties.TryGet(TestIdentityResourceAttributes.Version.Code, out var versionAttr).ShouldBeTrue();
        versionAttr.ShouldBeOfType<AttributeValue<int>>().TypedValue.ShouldBe(2);
    }

    [Fact]
    public async Task update_with_extended_properties_succeeds()
    {
        var admin = _fixture.IdentityResourceAdmin;
        var resource = new IdentityResourceConfiguration { Name = $"identity_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item;
        toUpdate.ExtendedProperties.Set(TestIdentityResourceAttributes.Owner, "security-team");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.ExtendedProperties.TryGet(TestIdentityResourceAttributes.Owner.Code, out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("security-team");
    }

    [Fact]
    public async Task update_with_unknown_attribute_returns_validation_error()
    {
        var admin = _fixture.IdentityResourceAdmin;
        var resource = new IdentityResourceConfiguration { Name = $"identity_{Guid.NewGuid():N}" };

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item;
        toUpdate.ExtendedProperties.Set(AttributeCode.Create("bad_attr"), "value");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task extended_properties_available_via_resource_store()
    {
        var admin = _fixture.IdentityResourceAdmin;
        var resourceStore = _fixture.ResourceStore;
        var name = $"identity_{Guid.NewGuid():N}";
        var resource = new IdentityResourceConfiguration { Name = name };
        resource.ExtendedProperties.Set(TestIdentityResourceAttributes.Owner, "platform-team");

        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var found = await resourceStore.FindIdentityResourcesByScopeNameAsync([name], _ct);
        var identityResource = found.ShouldHaveSingleItem();

        // Runtime Properties only contains string-typed extended attributes
        identityResource.Properties.ShouldContainKeyAndValue("owner", "platform-team");
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
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityResourceAdmin>();

        var resource = new IdentityResourceConfiguration { Name = $"identity_{Guid.NewGuid():N}" };
        resource.ExtendedProperties.Set(TestIdentityResourceAttributes.Owner, "team");

        var result = await admin.CreateAsync(resource, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task update_with_extended_properties_fails_when_no_schema_configured()
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
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityResourceAdmin>();

        // Create a resource without extended properties (succeeds)
        var resource = new IdentityResourceConfiguration { Name = $"identity_{Guid.NewGuid():N}" };
        var createResult = await admin.CreateAsync(resource, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        // Now update with extended properties (should fail)
        var toUpdate = getResult.Item;
        toUpdate.ExtendedProperties.Set(TestIdentityResourceAttributes.Owner, "team");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
