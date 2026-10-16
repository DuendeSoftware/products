// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SpaceExtendedPropertiesHowToTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly AttributeCode CompanyIdCode = AttributeCode.Create("company_id");
    private static readonly AttributeCode ThemeCode = AttributeCode.Create("theme");

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(services);
        services.AddStorageInternal(b => b.AddSqliteInMemory());

        // begin-snippet: space-extended-properties-schema
        var companyIdAttribute = new AttributeDefinition
        {
            Code = CompanyIdCode,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            IsUnique = true,
            IsQueryable = true
        };

        var themeAttribute = new AttributeDefinition
        {
            Code = ThemeCode,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            IsUnique = false,
            IsQueryable = true
        };

        var spaceSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.Space,
            DisplayName = "Space",
            Description = "Extended attributes for spaces.",
            AttributeDefinitions = [companyIdAttribute, themeAttribute]
        };
        // end-snippet

        // begin-snippet: space-extended-properties-register-schema-store
        // InMemorySchemaStore comes from Duende.Storage. A real product registers a schema
        // store backed by its own storage instance instead.
        services.AddSingleton<ISchemaStore>(new InMemorySchemaStore([spaceSchema]));
        // end-snippet

        _services = services.BuildServiceProvider();

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static Task<SaveResult<SpaceId>> CreateSpaceWithPropertiesAsync(
        ISpaceAdmin admin, string name, string origin, string companyId, string theme, Ct ct)
    {
        // begin-snippet: space-extended-properties-create
        var properties = new AttributeValueCollection();
        properties.Set(CompanyIdCode, companyId);
        properties.Set(ThemeCode, theme);

        return admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }],
                ExtendedProperties = properties
            },
            ct);
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task extended_properties_round_trip()
    {
        var created = await CreateSpaceWithPropertiesAsync(
            _admin, "Acme", "https://ext-props.example.com", "acme-corp", "dark", _ct);
        created.IsSuccess.ShouldBeTrue();

        var getResult = await _admin.GetAsync(created.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var properties = getResult.Item!.ExtendedProperties;
        properties.OfType<AttributeValue<string>>().First(a => a.Code == CompanyIdCode)
            .TypedValue.ShouldBe("acme-corp");
        properties.OfType<AttributeValue<string>>().First(a => a.Code == ThemeCode)
            .TypedValue.ShouldBe("dark");
    }

    [Fact]
    public async Task update_changes_a_single_extended_property()
    {
        var created = await CreateSpaceWithPropertiesAsync(
            _admin, "Acme", "https://ext-props-update.example.com", "acme-corp", "dark", _ct);
        created.IsSuccess.ShouldBeTrue();
        var id = created.Id!;
        var theme = "light";
        var admin = _admin;
        var ct = _ct;

        // begin-snippet: space-extended-properties-update
        var getResult = await admin.GetAsync(id, ct);
        var space = getResult.Item!;

        var properties = new AttributeValueCollection();
        foreach (var existing in space.ExtendedProperties)
        {
            if (existing.Code != ThemeCode)
            {
                properties.Set(existing);
            }
        }

        properties.Set(ThemeCode, theme);
        space.ExtendedProperties = properties.ToList();

        var updateResult = await admin.UpdateAsync(id, space, getResult.Version!, ct);
        // end-snippet
        updateResult.IsSuccess.ShouldBeTrue();

        var afterUpdate = await _admin.GetAsync(created.Id!, _ct);
        var afterUpdateProperties = afterUpdate.Item!.ExtendedProperties;
        afterUpdateProperties.OfType<AttributeValue<string>>().First(a => a.Code == ThemeCode)
            .TypedValue.ShouldBe("light");
        afterUpdateProperties.OfType<AttributeValue<string>>().First(a => a.Code == CompanyIdCode)
            .TypedValue.ShouldBe("acme-corp");
    }

    [Fact]
    public async Task duplicate_unique_value_fails_on_create()
    {
        var first = await CreateSpaceWithPropertiesAsync(
            _admin, "First", "https://ext-props-dup-1.example.com", "duplicate-co", "dark", _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await CreateSpaceWithPropertiesAsync(
            _admin, "Second", "https://ext-props-dup-2.example.com", "duplicate-co", "light", _ct);

        second.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task unknown_attribute_fails_validation()
    {
        var properties = new AttributeValueCollection();
        properties.Set(AttributeCode.Create("not_in_schema"), "value");

        var result = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Bad Attribute",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://ext-props-unknown.example.com" }],
                ExtendedProperties = properties
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task creating_without_a_registered_space_schema_fails_validation()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(serviceCollection);
        serviceCollection.AddStorageInternal(b => b.AddSqliteInMemory());
        // No schema registered for SchemaId.Space: AddStorageInternal falls back to an empty
        // ISchemaStore, so every attribute is rejected as undefined rather than accepted.
        await using var services = serviceCollection.BuildServiceProvider();

        var storageInstanceSchema = services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);
        var admin = services.GetRequiredService<ISpaceAdmin>();

        var properties = new AttributeValueCollection();
        properties.Set(CompanyIdCode, "acme-corp");

        var result = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "No Schema Store",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://ext-props-no-store.example.com" }],
                ExtendedProperties = properties
            },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Errors!.Single();
        error.Code.ShouldBe("validation_failed");
        error.Message.ShouldContain("not defined in the schema");
    }

    [Fact]
    public async Task deleted_spaces_unique_value_is_released_and_then_conflicts_on_undelete()
    {
        var created = await CreateSpaceWithPropertiesAsync(
            _admin, "Original", "https://ext-props-undelete-a.example.com", "contested-co", "dark", _ct);
        created.IsSuccess.ShouldBeTrue();
        var deletedId = created.Id!;

        var deleteResult = await _admin.DeleteAsync(deletedId, _ct);
        deleteResult.IsSuccess.ShouldBeTrue();

        // The unique value is released by delete: another space can now claim it.
        var claimant = await CreateSpaceWithPropertiesAsync(
            _admin, "Claimant", "https://ext-props-undelete-b.example.com", "contested-co", "light", _ct);
        claimant.IsSuccess.ShouldBeTrue();

        // begin-snippet: space-extended-properties-unique-conflict
        // UndeleteAsync fails with one "eav_conflict" error per unique value that another space
        // claimed while this one was deleted. The deleted space's own unique values are released
        // on delete, so a different space is free to claim them in the meantime.
        var undeleteResult = await _admin.UndeleteAsync(deletedId, _ct);
        // end-snippet

        undeleteResult.IsSuccess.ShouldBeFalse();
        undeleteResult.Errors.ShouldContain(e =>
            e.Code == "eav_conflict" &&
            e.Message.Contains("company_id", StringComparison.Ordinal) &&
            e.Message.Contains("contested-co", StringComparison.Ordinal) &&
            e.Message.Contains(claimant.Id!.Value.ToString(), StringComparison.Ordinal));

        var stillDeleted = await _admin.GetAsync(deletedId, _ct);
        stillDeleted.Item!.IsDeleted.ShouldBeTrue();
    }
}
