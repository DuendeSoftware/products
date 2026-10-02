// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Spaces.Internal.Licensing;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.Spaces;

public class SpacesLicenseValidatorTests
{
    private const string ExpectedMessage = "Your license does not include the Spaces feature.";
    private static Ct Ct => TestContext.Current.CancellationToken;

    public static TheoryData<SpaceId> UnentitledSpaceIds() =>
    [
        SpaceId.Default,
        SpaceId.Management,
        SpaceId.New()
    ];

    [Theory]
    [MemberData(nameof(UnentitledSpaceIds))]
    public void set_space_throws_when_unentitled(SpaceId spaceId)
    {
        var (services, spaceContext, _) = BuildUnentitledContainer();
        using var _ = services;

        var setSpace = () => spaceContext.SetSpace(spaceId);

        setSpace.ShouldThrow<Exception>().Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task get_storage_throws_when_unentitled()
    {
        var (services, _, _) = BuildUnentitledContainer();
        await using var _ = services;

        var partitionedStorageFactory = services.GetRequiredService<IPartitionedStorageFactory>();

        var exception = await Should.ThrowAsync<Exception>(async () => await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, Ct));
        exception.Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task create_throws_when_unentitled()
    {
        var (services, _, admin) = BuildUnentitledContainer();
        await using var _ = services;

        var createSpace = async () => await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Unentitled Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://unentitled.example.com" }]
            },
            Ct);

        var exception = await Should.ThrowAsync<Exception>(createSpace);
        exception.Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task get_throws_when_unentitled()
    {
        var (services, _, admin) = BuildUnentitledContainer();
        await using var _ = services;

        var getSpace = async () => await admin.GetAsync(SpaceId.New(), Ct);

        var exception = await Should.ThrowAsync<Exception>(getSpace);
        exception.Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task update_throws_when_unentitled()
    {
        var (services, _, admin) = BuildUnentitledContainer();
        await using var _ = services;

        var spaceId = SpaceId.New();
        var config = new SpaceConfiguration
        {
            Id = spaceId.Value,
            Name = "Unentitled Update",
            Enabled = true,
            PoolId = 1,
            MatchPatterns = [new SpaceMatchPattern { Origin = "https://unentitled-update.example.com" }]
        };

        var updateSpace = async () => await admin.UpdateAsync(spaceId, config, 1, Ct);

        var exception = await Should.ThrowAsync<Exception>(updateSpace);
        exception.Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task delete_throws_when_unentitled()
    {
        var (services, _, admin) = BuildUnentitledContainer();
        await using var _ = services;

        var deleteSpace = async () => await admin.DeleteAsync(SpaceId.New(), Ct);

        var exception = await Should.ThrowAsync<Exception>(deleteSpace);
        exception.Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task purge_throws_when_unentitled()
    {
        var (services, _, admin) = BuildUnentitledContainer();
        await using var _ = services;

        var purgeSpace = async () => await admin.PurgeAsync(SpaceId.New(), Ct);

        var exception = await Should.ThrowAsync<Exception>(purgeSpace);
        exception.Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task query_throws_when_unentitled()
    {
        var (services, _, admin) = BuildUnentitledContainer();
        await using var _ = services;

        var querySpace = async () => await admin.QueryAsync(
            QueryRequest.Create<SpaceFilter, SpaceSortField>(), Ct);

        var exception = await Should.ThrowAsync<Exception>(querySpace);
        exception.Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public void use_space_resolution_throws_when_unentitled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSpaces();
        TestSpacesLicense.RegisterUnentitled(builder.Services);
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemory());

        using var app = builder.Build();

        var useSpaceResolution = () => app.UseSpaceResolution();

        useSpaceResolution.ShouldThrow<Exception>().Message.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public void use_space_resolution_succeeds_when_entitled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSpaces();
        TestSpacesLicense.RegisterEntitled(builder.Services);
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemory());

        using var app = builder.Build();

        var useSpaceResolution = () => app.UseSpaceResolution();

        useSpaceResolution.ShouldNotThrow();
    }

    [Fact]
    public async Task create_over_limit_logs_and_still_succeeds()
    {
        var (services, admin, logs) = await BuildEntitledWithLimitContainerAsync(spaceLimit: 1);
        await using var _ = services;

        var first = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "First Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://license-count-first.example.com" }]
            },
            Ct);

        first.IsSuccess.ShouldBeTrue();

        var startIndex = logs.GetSnapshot().Count;

        var result = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Second Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://license-count-second.example.com" }]
            },
            Ct);

        result.IsSuccess.ShouldBeTrue();
        HasQuantizedOverLimitRecord(logs, startIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task undelete_over_limit_logs_and_still_succeeds()
    {
        var (services, admin, logs) = await BuildEntitledWithLimitContainerAsync(spaceLimit: 1);
        await using var _ = services;

        var first = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Undelete First",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://license-count-undelete-first.example.com" }]
            },
            Ct);
        first.IsSuccess.ShouldBeTrue();

        await admin.DeleteAsync(first.Id!, Ct);

        var second = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Undelete Second",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://license-count-undelete-second.example.com" }]
            },
            Ct);
        second.IsSuccess.ShouldBeTrue();

        var startIndex = logs.GetSnapshot().Count;

        var undeleteResult = await admin.UndeleteAsync(first.Id!, Ct);

        undeleteResult.IsSuccess.ShouldBeTrue();
        HasQuantizedOverLimitRecord(logs, startIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task validate_count_swallows_count_delegate_exception()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpaces();
        TestSpacesLicense.RegisterEntitledWithSpaceLimit(services, spaceLimit: 1, out _);
        await using var serviceProvider = services.BuildServiceProvider();

        var validator = serviceProvider.GetRequiredService<SpacesLicenseValidator>();

        var act = async () => await validator.ValidateSpaceCountAsync(
            _ => throw new InvalidOperationException("boom"),
            Ct.None);

        await act.ShouldNotThrowAsync();
    }

    private static (ServiceProvider Services, ISpaceContextAccessor SpaceContext, ISpaceAdmin Admin)
        BuildUnentitledContainer()
    {
        var serviceProvider = BuildServiceProvider(dataSourceName: null, entitled: false);
        var spaceContext = serviceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = serviceProvider.GetRequiredService<ISpaceAdmin>();
        return (serviceProvider, spaceContext, admin);
    }

    private static ServiceProvider BuildServiceProvider(string? dataSourceName, bool entitled)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpaces();
        if (entitled)
        {
            TestSpacesLicense.RegisterEntitled(services);
        }
        else
        {
            TestSpacesLicense.RegisterUnentitled(services);
        }

        services.AddStorageInternal(b =>
        {
            if (dataSourceName is null)
            {
                b.AddSqliteInMemory();
            }
            else
            {
                b.AddSqliteInMemory(dataSourceName);
            }
        });
        return services.BuildServiceProvider();
    }

    private static async Task<(ServiceProvider Services, ISpaceAdmin Admin, FakeLogCollector Logs)>
        BuildEntitledWithLimitContainerAsync(int spaceLimit)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpaces();
        TestSpacesLicense.RegisterEntitledWithSpaceLimit(services, spaceLimit, out var logs);
        services.AddStorageInternal(b => b.AddSqliteInMemory());
        var serviceProvider = services.BuildServiceProvider();

        var storageInstanceSchema = serviceProvider.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(Ct);

        var admin = serviceProvider.GetRequiredService<ISpaceAdmin>();
        return (serviceProvider, admin, logs);
    }

    private static bool HasQuantizedOverLimitRecord(FakeLogCollector logs, int startIndex)
    {
        // The underlying LicenseValidator logs the entitlement name (not the raw SKU id) in its
        // structured state when a quantized limit is exceeded. Matching on "Space Count" ties
        // the assertion to the space-count quantized path specifically, so an unrelated warning
        // from another code path would not satisfy this check.
        var snapshot = logs.GetSnapshot();

        for (var i = startIndex; i < snapshot.Count; i++)
        {
            var record = snapshot[i];
            if (record.Level < LogLevel.Warning)
            {
                continue;
            }

            var state = record.StructuredState;
            if (state is null)
            {
                continue;
            }

            if (state.Any(kv => kv.Value?.ToString() == "Space Count"))
            {
                return true;
            }
        }

        return false;
    }
}
