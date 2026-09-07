// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.SamlServiceProviders;

/// <summary>
/// Integration tests verifying that <see cref="SamlServiceProviderConfiguration.ExtendedProperties"/>
/// are validated against the schema (<c>saml_service_provider</c>) and round-trip correctly
/// through the admin store.
/// </summary>
public sealed class SamlServiceProviderExtendedPropertiesTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static CreateSamlServiceProvider CreateMinimalConfig() =>
        new()
        {
            EntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

    [Fact]
    public async Task create_with_valid_extended_properties_round_trips_correctly()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var sp = CreateMinimalConfig();
        sp.ExtendedProperties.Set(TestSamlServiceProviderAttributes.Environment, "production");

        var createResult = await admin.CreateAsync(sp, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.ExtendedProperties.Count.ShouldBe(1);
        loaded.ExtendedProperties.TryGet(TestSamlServiceProviderAttributes.Environment.Code, out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("production");
    }

    [Fact]
    public async Task create_with_unknown_attribute_returns_validation_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var sp = CreateMinimalConfig();
        sp.ExtendedProperties.Set(AttributeCode.Create("unknown_attr"), "value");

        var result = await admin.CreateAsync(sp, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task create_with_empty_extended_properties_and_no_schema_succeeds()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var dbName = $"test_{Guid.NewGuid():N}";

        services.AddIdentityServer()
            .AddStorage(storage =>
                storage.AddSqliteStore(opt =>
                    opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"))
            .AddSamlServiceProviderConfigurationValidator<NopSamlServiceProviderConfigurationValidator>();

        services.AddSingleton<ISchemaStore>(new InMemorySchemaStore([]));

        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<ISamlServiceProviderAdmin>();

        var sp = CreateMinimalConfig();
        // No extended properties set

        var result = await admin.CreateAsync(sp, _ct);
        result.IsSuccess.ShouldBeTrue($"Create with empty extended properties failed: {result}");
    }

    [Fact]
    public async Task create_with_extended_properties_fails_when_no_schema_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var dbName = $"test_{Guid.NewGuid():N}";

        services.AddIdentityServer()
            .AddStorage(storage =>
                storage.AddSqliteStore(opt =>
                    opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"))
            .AddSamlServiceProviderConfigurationValidator<NopSamlServiceProviderConfigurationValidator>();

        services.AddSingleton<ISchemaStore>(new InMemorySchemaStore([]));

        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<ISamlServiceProviderAdmin>();

        var sp = CreateMinimalConfig();
        sp.ExtendedProperties.Set(AttributeCode.Create("some_attr"), "value");

        var result = await admin.CreateAsync(sp, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task update_with_valid_extended_properties_round_trips_correctly()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var sp = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(sp, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ExtendedProperties.Set(TestSamlServiceProviderAttributes.Environment, "staging");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.ExtendedProperties.TryGet(TestSamlServiceProviderAttributes.Environment.Code, out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("staging");
    }

    [Fact]
    public async Task update_with_invalid_attribute_returns_validation_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var sp = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(sp, _ct);
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

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
