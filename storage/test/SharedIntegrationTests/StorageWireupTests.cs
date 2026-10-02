// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.IntegrationTests;

public partial class StorageWireupTests
{
    private static readonly EntityType EntityType = TestDso.DsoVersion.EntityType;
    private readonly StorageInstanceId _someStorageInstanceId = StorageInstanceId.Create("some_instance");
    private readonly DataCategoryName _someDataCategory = DataCategoryName.Create("some_category");
    private readonly UuidV7 _id = UuidV7.New();
    private readonly TestDso _testValue = new($"{nameof(_testValue)}-{Guid.NewGuid()}");

    // You can add and use a separate storage category
    // you can add and use a separate instance
    // When linked to a separate instance, data goes to that separate instance
    // when not linked to a separate instance, data goes to the default instance.

    [Fact]
    public async Task
        Given_separate_StorageInstance_that_is_not_linked_to_a_category_then_data_written_to_default_instance()
    {
        await using var fixture = await CreateProviderAsync();

        var defaultStorage = fixture.PartitionedStorage;
        var someCategoryStorage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(_someDataCategory, _ct);

        (await someCategoryStorage.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(
            CreateResult.Success);

        (await someCategoryStorage.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBe(true);
        (await defaultStorage.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBe(true);
    }

    [Fact]
    public async Task Given_separate_StorageInstance_linked_to_category_then_data_written_to_that_instance()
    {
        await using var fixture = await CreateProviderAsync(services =>
        {
            services.GetOrAddStorageInstanceRouter().AddMapping(_someDataCategory, _someStorageInstanceId);
        });

        var defaultStorage = fixture.PartitionedStorage;
        var someCategoryStorage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(_someDataCategory, _ct);

        (await someCategoryStorage.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(
            CreateResult.Success);

        (await someCategoryStorage.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBe(true);
        (await defaultStorage.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBe(false);
    }

    [Fact]
    public async Task Given_only_a_named_StorageInstance_registered_then_GetAll_does_not_include_Default()
    {
        // Regression test: GetAll() used to unconditionally append StorageInstanceId.Default,
        // even when only a named instance was ever registered. Consumers (e.g. OutboxProcessor)
        // iterate GetAll() and would then try to resolve a Default instance that was never
        // configured, aborting valid named-instance processing.
        //
        // Built directly against a bare ServiceCollection (rather than via CreateProviderAsync/
        // FixtureFactory.CreateAsync) because those helpers always additionally register a
        // Default instance to back fixture.PartitionedStorage.
        var services = new ServiceCollection();
        _ = services.AddLogging();
        services.AddDsoRegistration<TestDso>();
        _ = services.AddStorageInternal(_someStorageInstanceId, FixtureFactory.AddStorageInstance);

        await using var provider = services.BuildServiceProvider();
        var storageInstanceRouter = provider.GetRequiredService<IStorageInstanceRouter>();

        storageInstanceRouter.GetAll().ShouldBe([_someStorageInstanceId]);
    }

    [Fact]
    public async Task Given_multiple_categories_mapped_to_the_same_instance_then_GetAll_returns_that_instance_once()
    {
        var otherCategory = DataCategoryName.Create("some_other_category");

        var services = new ServiceCollection();
        _ = services.AddLogging();
        services.AddDsoRegistration<TestDso>();
        _ = services.AddStorageInternal(_someStorageInstanceId, FixtureFactory.AddStorageInstance);
        var storageInstanceRouter = services.GetOrAddStorageInstanceRouter();
        storageInstanceRouter.AddMapping(_someDataCategory, _someStorageInstanceId);
        storageInstanceRouter.AddMapping(otherCategory, _someStorageInstanceId);

        await using var provider = services.BuildServiceProvider();
        var storageInstanceRouterFromContainer = provider.GetRequiredService<IStorageInstanceRouter>();

        storageInstanceRouterFromContainer.GetAll().ShouldBe([_someStorageInstanceId]);
    }

    [Fact]
    public async Task Given_a_configured_Default_StorageInstance_then_GetAll_includes_it_exactly_once()
    {
        await using var fixture = await FixtureFactory.CreateAsync(_ct, services => services.AddDsoRegistration<TestDso>());

        var storageInstanceRouter = fixture.ServiceProvider.GetRequiredService<IStorageInstanceRouter>();

        storageInstanceRouter.GetAll().ShouldBe([StorageInstanceId.Default]);
    }

    [Fact]
    public async Task Given_a_named_instance_and_a_configured_Default_then_GetAll_returns_both_exactly_once()
    {
        await using var fixture = await CreateProviderAsync();

        var storageInstanceRouter = fixture.ServiceProvider.GetRequiredService<IStorageInstanceRouter>();

        storageInstanceRouter.GetAll().ShouldBe([_someStorageInstanceId, StorageInstanceId.Default]);
    }

    [Fact]
    public async Task Should_not_be_able_to_register_same_provider_twice()
    {
        var storageInstanceId = StorageInstanceId.Create("other_instance");
        await using var fixture = await CreateProviderAsync(x =>
        {
            _ = x.AddStorageInternal(storageInstanceId, b =>
            {
                FixtureFactory.AddStorageInstance(b);
                _ = Should.Throw<Exception>(() => FixtureFactory.AddStorageInstance(b));
            });
        });
    }

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<IStorageFixture> CreateProviderAsync(Action<IServiceCollection>? addMappings = null) =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddStorageInternal(_someStorageInstanceId, FixtureFactory.AddStorageInstance);
            addMappings?.Invoke(services);
            services.AddDsoRegistration<TestDso>();
        });
}
