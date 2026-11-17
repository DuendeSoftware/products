// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.DataExtensions.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SpaceExtendedPropertiesWithDatabaseSchemaTests : IAsyncLifetime
{
    private static readonly AttributeCode BrandColor = AttributeCode.Create("brandColor");

    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private ISpaceContextAccessor _accessor = null!;
    private SpaceId _globex = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public enum ActiveSpace
    {
        None,
        Management,
        Tenant,
        Default
    }

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDynamicSchemaStorage();
        services.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterEntitled(services);
        services.AddStorageInternal(b => b.AddSqliteInMemory());
        _services = services.BuildServiceProvider();

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
        _accessor = _services.GetRequiredService<ISpaceContextAccessor>();

        _globex = await CreateSpaceAsync(new CreateSpaceConfiguration
        {
            Name = "Globex",
            MatchPatterns = [new SpaceMatchPattern { Path = "/globex" }]
        });
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Theory]
    [InlineData(ActiveSpace.None)]
    [InlineData(ActiveSpace.Management)]
    [InlineData(ActiveSpace.Tenant)]
    public async Task
        when_an_admin_reads_a_space_extended_property_defined_in_the_database_expect_the_same_value_in_every_active_space(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        var acme = await CreateSpaceAsync(AcmeWithBrandColor());

        Duende.IdentityServer.Models.GetResult<SpaceConfiguration> result;
        using (Activate(activeSpace))
        {
            result = await _admin.GetAsync(acme, _ct);
        }

        result.Found.ShouldBeTrue();
        result.Item!.ExtendedProperties.Single(p => p.Code == BrandColor).UntypedValue.ShouldBe("#0a7");
    }

    [Theory]
    [InlineData(ActiveSpace.None)]
    [InlineData(ActiveSpace.Management)]
    [InlineData(ActiveSpace.Tenant)]
    public async Task
        when_an_admin_updates_a_space_extended_property_defined_in_the_database_expect_the_new_value_in_every_active_space(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        var acme = await CreateSpaceAsync(AcmeWithBrandColor());
        var current = await _admin.GetAsync(acme, _ct);
        var space = current.Item!;
        var brandColor = new AttributeValueCollection();
        brandColor.Set(BrandColor, "#c33");
        space.ExtendedProperties = brandColor.ToList();

        Duende.IdentityServer.Models.SaveResult<SpaceId> updated;
        using (Activate(activeSpace))
        {
            updated = await _admin.UpdateAsync(acme, space, current.Version!, _ct);
        }

        updated.IsSuccess.ShouldBeTrue($"Update failed: {updated}");
        Duende.IdentityServer.Models.GetResult<SpaceConfiguration> read;
        using (Activate(ActiveSpace.Management))
        {
            read = await _admin.GetAsync(acme, _ct);
        }

        read.Item!.ExtendedProperties.Single(p => p.Code == BrandColor).UntypedValue.ShouldBe("#c33");
    }

    [Theory]
    [InlineData(ActiveSpace.None)]
    [InlineData(ActiveSpace.Management)]
    [InlineData(ActiveSpace.Tenant)]
    public async Task
        when_an_admin_restores_a_deleted_space_with_a_database_extended_property_expect_success_in_every_active_space(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        var acme = await CreateSpaceAsync(AcmeWithBrandColor());
        (await _admin.DeleteAsync(acme, _ct)).IsSuccess.ShouldBeTrue();

        Duende.IdentityServer.Models.SaveResult<SpaceId> restored;
        using (Activate(activeSpace))
        {
            restored = await _admin.UndeleteAsync(acme, _ct);
        }

        restored.IsSuccess.ShouldBeTrue($"Undelete failed: {restored}");
    }

    [Theory]
    [InlineData(ActiveSpace.None)]
    [InlineData(ActiveSpace.Management)]
    [InlineData(ActiveSpace.Tenant)]
    public async Task
        when_an_admin_creates_a_space_with_a_database_extended_property_expect_success_in_every_active_space(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        Duende.IdentityServer.Models.SaveResult<SpaceId> created;
        using (Activate(activeSpace))
        {
            created = await _admin.CreateAsync(AcmeWithBrandColor(), _ct);
        }

        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
    }

    [Theory]
    [InlineData(ActiveSpace.None)]
    [InlineData(ActiveSpace.Management)]
    public async Task
        when_an_admin_creates_the_space_schema_expect_it_stored_in_the_management_space(
            ActiveSpace activeSpace)
    {
        using (Activate(activeSpace))
        {
            var created = await _services.GetRequiredService<ISchemaAdmin>()
                .CreateAsync(SpaceSchemaWithBrandColor(), _ct);
            created.IsSuccess.ShouldBeTrue($"Create schema failed: {created}");
        }

        IReadOnlyAttributeSchema? stored;
        using (_accessor.SetSpace(SpaceId.Management))
        {
            stored = await _services.GetRequiredService<ISchemaStore>().GetAsync(SchemaId.Space, _ct);
        }

        stored!.AttributeDefinitions.ShouldContainKey(BrandColor);
    }

    [Fact]
    public async Task when_an_admin_gets_the_space_schema_in_a_tenant_space_expect_the_management_copy()
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> result;
        using (Activate(ActiveSpace.Tenant))
        {
            result = await _services.GetRequiredService<ISchemaAdmin>().GetAsync(SchemaId.Space, _ct);
        }

        result.Found.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ActiveSpace.Tenant)]
    [InlineData(ActiveSpace.Default)]
    public async Task
        when_an_admin_creates_the_space_schema_from_another_space_expect_a_validation_failure(
            ActiveSpace activeSpace)
    {
        var schemaAdmin = _services.GetRequiredService<ISchemaAdmin>();

        Duende.IdentityServer.Models.SaveResult<SchemaId> created;
        using (Activate(activeSpace))
        {
            created = await schemaAdmin.CreateAsync(SpaceSchemaWithBrandColor(), _ct);
        }

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> inManagement;
        using (Activate(ActiveSpace.Management))
        {
            inManagement = await schemaAdmin.GetAsync(SchemaId.Space, _ct);
        }

        created.IsSuccess.ShouldBeFalse();
        created.Errors!.ShouldContain(e => e.Code == "validation_failed");
        inManagement.Found.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ActiveSpace.None)]
    [InlineData(ActiveSpace.Management)]
    public async Task
        when_an_admin_updates_the_space_schema_expect_the_management_copy_updated(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        var schemaAdmin = _services.GetRequiredService<ISchemaAdmin>();
        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> current;
        using (Activate(ActiveSpace.Management))
        {
            current = await schemaAdmin.GetAsync(SchemaId.Space, _ct);
        }

        var schema = current.Item!;
        schema.DisplayName = "Space (updated)";
        Duende.IdentityServer.Models.SaveResult<SchemaId> updated;
        using (Activate(activeSpace))
        {
            updated = await schemaAdmin.UpdateAsync(SchemaId.Space, schema, current.Version!, _ct);
        }

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> read;
        using (Activate(ActiveSpace.Management))
        {
            read = await schemaAdmin.GetAsync(SchemaId.Space, _ct);
        }

        updated.IsSuccess.ShouldBeTrue($"Update schema failed: {updated}");
        read.Item!.DisplayName.ShouldBe("Space (updated)");
    }
    [Theory]
    [InlineData(ActiveSpace.Tenant)]
    [InlineData(ActiveSpace.Default)]
    public async Task
        when_an_admin_updates_the_space_schema_from_another_space_expect_a_validation_failure(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        var schemaAdmin = _services.GetRequiredService<ISchemaAdmin>();
        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> current;
        using (Activate(ActiveSpace.Management))
        {
            current = await schemaAdmin.GetAsync(SchemaId.Space, _ct);
        }

        var schema = current.Item!;
        var originalDisplayName = schema.DisplayName;
        schema.DisplayName = "Space (updated)";
        Duende.IdentityServer.Models.SaveResult<SchemaId> updated;
        using (Activate(activeSpace))
        {
            updated = await schemaAdmin.UpdateAsync(SchemaId.Space, schema, current.Version!, _ct);
        }

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> read;
        using (Activate(ActiveSpace.Management))
        {
            read = await schemaAdmin.GetAsync(SchemaId.Space, _ct);
        }

        updated.IsSuccess.ShouldBeFalse();
        updated.Errors!.ShouldContain(e => e.Code == "validation_failed");
        read.Item!.DisplayName.ShouldBe(originalDisplayName);
    }

    [Theory]
    [InlineData(ActiveSpace.None)]
    [InlineData(ActiveSpace.Management)]
    public async Task
        when_an_admin_deletes_the_space_schema_expect_the_management_copy_deleted(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        var schemaAdmin = _services.GetRequiredService<ISchemaAdmin>();

        Duende.IdentityServer.Models.SaveResult<SchemaId> deleted;
        using (Activate(activeSpace))
        {
            deleted = await schemaAdmin.DeleteAsync(SchemaId.Space, _ct);
        }

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> read;
        using (Activate(ActiveSpace.Management))
        {
            read = await schemaAdmin.GetAsync(SchemaId.Space, _ct);
        }

        deleted.IsSuccess.ShouldBeTrue($"Delete schema failed: {deleted}");
        read.Found.ShouldBeFalse();
    }
    [Theory]
    [InlineData(ActiveSpace.Tenant)]
    [InlineData(ActiveSpace.Default)]
    public async Task
        when_an_admin_deletes_the_space_schema_from_another_space_expect_a_validation_failure(
            ActiveSpace activeSpace)
    {
        await CreateSchemaAsync(SpaceSchemaWithBrandColor());
        var schemaAdmin = _services.GetRequiredService<ISchemaAdmin>();

        Duende.IdentityServer.Models.SaveResult<SchemaId> deleted;
        using (Activate(activeSpace))
        {
            deleted = await schemaAdmin.DeleteAsync(SchemaId.Space, _ct);
        }

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> read;
        using (Activate(ActiveSpace.Management))
        {
            read = await schemaAdmin.GetAsync(SchemaId.Space, _ct);
        }

        deleted.IsSuccess.ShouldBeFalse();
        deleted.Errors!.ShouldContain(e => e.Code == "validation_failed");
        read.Found.ShouldBeTrue();
    }

    [Fact]
    public async Task when_an_admin_creates_a_customer_schema_in_a_tenant_space_expect_it_to_stay_in_that_tenant()
    {
        var loyalty = new SchemaConfiguration
        {
            SchemaId = SchemaId.Create("loyalty"),
            AttributeDefinitions =
            [
                new AttributeDefinition
                {
                    Code = BrandColor,
                    AttributeType = new ScalarAttributeType(ScalarDataType.String)
                }
            ]
        };
        var schemaAdmin = _services.GetRequiredService<ISchemaAdmin>();

        using (Activate(ActiveSpace.Tenant))
        {
            (await schemaAdmin.CreateAsync(loyalty, _ct)).IsSuccess.ShouldBeTrue();
        }

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> inTenant;
        using (Activate(ActiveSpace.Tenant))
        {
            inTenant = await schemaAdmin.GetAsync(loyalty.SchemaId, _ct);
        }

        Duende.IdentityServer.Models.GetResult<SchemaConfiguration> inManagement;
        using (Activate(ActiveSpace.Management))
        {
            inManagement = await schemaAdmin.GetAsync(loyalty.SchemaId, _ct);
        }

        inTenant.Found.ShouldBeTrue();
        inManagement.Found.ShouldBeFalse();
    }

    private static SchemaConfiguration SpaceSchemaWithBrandColor() =>
        new()
        {
            SchemaId = SchemaId.Space,
            AttributeDefinitions =
            [
                new AttributeDefinition
                {
                    Code = BrandColor,
                    AttributeType = new ScalarAttributeType(ScalarDataType.String)
                }
            ]
        };

    private static CreateSpaceConfiguration AcmeWithBrandColor()
    {
        var extendedProperties = new AttributeValueCollection();
        extendedProperties.Set(BrandColor, "#0a7");
        return new CreateSpaceConfiguration
        {
            Name = "Acme",
            MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }],
            ExtendedProperties = extendedProperties
        };
    }

    private async Task CreateSchemaAsync(SchemaConfiguration schema)
    {
        using (_accessor.SetSpace(SpaceId.Management))
        {
            var created = await _services.GetRequiredService<ISchemaAdmin>()
                .CreateAsync(schema, _ct);
            created.IsSuccess.ShouldBeTrue($"Create schema failed: {created}");
        }
    }

    private async Task<SpaceId> CreateSpaceAsync(CreateSpaceConfiguration configuration)
    {
        using (_accessor.SetSpace(SpaceId.Management))
        {
            var created = await _admin.CreateAsync(configuration, _ct);
            created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
            return created.Id!;
        }
    }

    private IDisposable? Activate(ActiveSpace activeSpace) =>
        activeSpace switch
        {
            ActiveSpace.Management => _accessor.SetSpace(SpaceId.Management),
            ActiveSpace.Tenant => _accessor.SetSpace(_globex),
            ActiveSpace.Default => _accessor.SetSpace(SpaceId.Default),
            _ => null
        };
}
