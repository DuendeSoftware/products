// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SpaceStoreExtendedPropertiesTests
{
    private static readonly AttributeCode BrandColorCode = AttributeCode.Create("brand_color");
    private static readonly AttributeCode SeatsCode = AttributeCode.Create("seats");
    private static readonly AttributeCode VerifiedCode = AttributeCode.Create("verified");
    private static readonly AttributeCode DiscountCode = AttributeCode.Create("discount");
    private static readonly AttributeCode RenewalDateCode = AttributeCode.Create("renewal_date");
    private static readonly AttributeCode ActivatedAtCode = AttributeCode.Create("activated_at");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task when_requesting_a_path_of_a_space_with_extended_properties_expect_200_in_that_space()
    {
        await using var node = await StartNodeAsync(
            databaseName: NewDatabaseName(),
            sharedCache: null,
            spaceSchema: SpaceSchema(seatsDataType: ScalarDataType.Integer));
        var acmeSpaceId = await CreateAcmeSpaceAsync(node);
        using var client = new HttpClient { BaseAddress = new Uri(node.Urls.First()) };

        var currentSpaceResponse = await client.GetAsync("/t/acme/current-space", _ct);

        currentSpaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await currentSpaceResponse.Content.ReadAsStringAsync(_ct)).ShouldBe(acmeSpaceId.Value.ToString());
    }

    [Fact]
    public async Task when_reading_a_space_by_id_twice_expect_the_extended_properties_that_the_admin_reads()
    {
        await using var node = await StartNodeAsync(
            databaseName: NewDatabaseName(),
            sharedCache: null,
            spaceSchema: SpaceSchema(seatsDataType: ScalarDataType.Integer));
        var acmeSpaceId = await CreateAcmeSpaceAsync(node);
        var spaceStore = node.Services.GetRequiredService<ISpaceStore>();
        var adminSpace = (await node.Services.GetRequiredService<ISpaceAdmin>().GetAsync(acmeSpaceId, _ct)).Item!;

        var spaceOnCacheMiss = await spaceStore.TryGetSpace(acmeSpaceId, _ct);
        var spaceOnCacheHit = await spaceStore.TryGetSpace(acmeSpaceId, _ct);

        spaceOnCacheMiss!.ExtendedProperties.ShouldBe(adminSpace.ExtendedProperties, ignoreOrder: true);
        spaceOnCacheHit!.ExtendedProperties.ShouldBe(adminSpace.ExtendedProperties, ignoreOrder: true);
    }

    [Fact]
    public async Task when_reading_a_space_by_pool_id_twice_expect_the_extended_properties_that_the_admin_reads()
    {
        await using var node = await StartNodeAsync(
            databaseName: NewDatabaseName(),
            sharedCache: null,
            spaceSchema: SpaceSchema(seatsDataType: ScalarDataType.Integer));
        var acmeSpaceId = await CreateAcmeSpaceAsync(node);
        var spaceStore = node.Services.GetRequiredService<ISpaceStore>();
        var adminSpace = (await node.Services.GetRequiredService<ISpaceAdmin>().GetAsync(acmeSpaceId, _ct)).Item!;

        var spaceOnCacheMiss = await spaceStore.TryGetSpaceByPoolId(adminSpace.PoolId, _ct);
        var spaceOnCacheHit = await spaceStore.TryGetSpaceByPoolId(adminSpace.PoolId, _ct);

        spaceOnCacheMiss!.ExtendedProperties.ShouldBe(adminSpace.ExtendedProperties, ignoreOrder: true);
        spaceOnCacheHit!.ExtendedProperties.ShouldBe(adminSpace.ExtendedProperties, ignoreOrder: true);
    }

    [Fact]
    public async Task when_a_node_without_an_attribute_in_its_space_schema_reads_a_space_cached_by_a_node_with_it_expect_the_attribute_dropped_like_the_admin_does()
    {
        await using var sharedCacheServices = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        var sharedCache = sharedCacheServices.GetRequiredService<HybridCache>();
        var sharedDatabaseName = NewDatabaseName();
        await using var nodeWithSeats = await StartNodeAsync(
            databaseName: sharedDatabaseName,
            sharedCache: sharedCache,
            spaceSchema: SpaceSchema(seatsDataType: ScalarDataType.Integer));
        var acmeSpaceId = await CreateAcmeSpaceAsync(nodeWithSeats);
        (await nodeWithSeats.Services.GetRequiredService<ISpaceStore>().TryGetSpace(acmeSpaceId, _ct))!
            .ExtendedProperties.ShouldContain(attribute => attribute.Code == SeatsCode);

        await using var nodeWithoutSeats = await StartNodeAsync(
            databaseName: sharedDatabaseName,
            sharedCache: sharedCache,
            spaceSchema: SpaceSchema(seatsDataType: null));
        var adminSpaceWithoutSeats = (await nodeWithoutSeats.Services.GetRequiredService<ISpaceAdmin>().GetAsync(acmeSpaceId, _ct)).Item!;

        var spaceFromSharedCache = await nodeWithoutSeats.Services.GetRequiredService<ISpaceStore>().TryGetSpace(acmeSpaceId, _ct);

        spaceFromSharedCache!.ExtendedProperties.ShouldBe(adminSpaceWithoutSeats.ExtendedProperties, ignoreOrder: true);
        spaceFromSharedCache.ExtendedProperties.ShouldNotContain(attribute => attribute.Code == SeatsCode);
    }

    [Fact]
    public async Task when_a_node_with_a_changed_attribute_type_in_its_space_schema_reads_a_space_cached_by_a_node_with_the_old_type_expect_the_value_typed_like_the_admin_does()
    {
        await using var sharedCacheServices = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        var sharedCache = sharedCacheServices.GetRequiredService<HybridCache>();
        var sharedDatabaseName = NewDatabaseName();
        await using var nodeWithIntegerSeats = await StartNodeAsync(
            databaseName: sharedDatabaseName,
            sharedCache: sharedCache,
            spaceSchema: SpaceSchema(seatsDataType: ScalarDataType.Integer));
        var acmeSpaceId = await CreateAcmeSpaceAsync(nodeWithIntegerSeats);
        (await nodeWithIntegerSeats.Services.GetRequiredService<ISpaceStore>().TryGetSpace(acmeSpaceId, _ct))!
            .ExtendedProperties.ShouldContain(attribute => attribute is AttributeValue<int> && attribute.Code == SeatsCode);

        await using var nodeWithDecimalSeats = await StartNodeAsync(
            databaseName: sharedDatabaseName,
            sharedCache: sharedCache,
            spaceSchema: SpaceSchema(seatsDataType: ScalarDataType.Decimal));
        var adminSpaceWithDecimalSeats = (await nodeWithDecimalSeats.Services.GetRequiredService<ISpaceAdmin>().GetAsync(acmeSpaceId, _ct)).Item!;

        var spaceFromSharedCache = await nodeWithDecimalSeats.Services.GetRequiredService<ISpaceStore>().TryGetSpace(acmeSpaceId, _ct);

        spaceFromSharedCache!.ExtendedProperties.ShouldBe(adminSpaceWithDecimalSeats.ExtendedProperties, ignoreOrder: true);
        spaceFromSharedCache.ExtendedProperties.ShouldContain(attribute => attribute is AttributeValue<decimal> && attribute.Code == SeatsCode);
    }

    private static string NewDatabaseName() => $"SpaceStoreExtendedProperties_{Guid.NewGuid():N}";

    private static SchemaConfiguration SpaceSchema(ScalarDataType? seatsDataType)
    {
        var spaceSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.Space,
            DisplayName = "Space",
            AttributeDefinitions =
            [
                ScalarAttribute(BrandColorCode, ScalarDataType.String),
                ScalarAttribute(VerifiedCode, ScalarDataType.Boolean),
                ScalarAttribute(DiscountCode, ScalarDataType.Decimal),
                ScalarAttribute(RenewalDateCode, ScalarDataType.Date),
                ScalarAttribute(ActivatedAtCode, ScalarDataType.DateTime)
            ]
        };

        if (seatsDataType is { } dataType)
        {
            spaceSchema.AttributeDefinitions.Add(ScalarAttribute(SeatsCode, dataType));
        }

        return spaceSchema;
    }

    private static AttributeDefinition ScalarAttribute(AttributeCode code, ScalarDataType dataType) => new()
    {
        Code = code,
        AttributeType = new ScalarAttributeType(dataType)
    };

    private async Task<SpaceId> CreateAcmeSpaceAsync(WebApplication node)
    {
        var acmeProperties = new AttributeValueCollection();
        acmeProperties.Set(BrandColorCode, "#ff6600");
        acmeProperties.Set(SeatsCode, 25);
        acmeProperties.Set(VerifiedCode, true);
        acmeProperties.Set(DiscountCode, 12.50m);
        acmeProperties.Set(RenewalDateCode, new DateOnly(2027, 3, 31));
        acmeProperties.Set(ActivatedAtCode, new DateTimeOffset(2026, 11, 11, 9, 30, 15, TimeSpan.FromHours(2)));

        var acmeSpaceCreation = await node.Services.GetRequiredService<ISpaceAdmin>().CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }],
                ExtendedProperties = acmeProperties
            },
            _ct);

        acmeSpaceCreation.IsSuccess.ShouldBeTrue();
        return acmeSpaceCreation.Id!;
    }

    private async Task<WebApplication> StartNodeAsync(string databaseName, HybridCache? sharedCache, SchemaConfiguration spaceSchema)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterEntitled(builder.Services);
        builder.Services.AddStorageInternal(storage => storage.AddSqliteInMemory(databaseName));
        builder.Services.AddSingleton<ISchemaStore>(new InMemorySchemaStore([spaceSchema]));
        if (sharedCache is not null)
        {
            builder.Services.AddKeyedSingleton(SpacesServiceProviderKeys.SpacesCache, sharedCache);
        }

        var node = builder.Build();
        await node.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(_ct);
        node.UseSpaceResolution();
        node.UseRouting();
        node.MapGet("/current-space", (ISpaceContextAccessor spaceContext) => spaceContext.GetSpaceId().Value.ToString());

        await node.StartAsync(_ct);
        return node;
    }
}
