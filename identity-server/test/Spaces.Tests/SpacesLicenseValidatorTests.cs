// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Spaces.Internal;
using Duende.Spaces.Internal.Licensing;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using Duende.IdentityServer.Models;
using DataCategoryName = Duende.Storage.DataCategoryName;

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
        builder.Services.AddIdentityServerBuilder().AddSpaces(_ => { });
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
        builder.Services.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterEntitled(builder.Services);
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemory());

        using var app = builder.Build();

        var useSpaceResolution = () => app.UseSpaceResolution();

        useSpaceResolution.ShouldNotThrow();
    }

    [Fact]
    public async Task create_over_limit_logs_and_still_succeeds()
    {
        var timeProvider = new FakeTimeProvider();
        var (services, admin, logs) = await BuildEntitledWithLimitContainerAsync(spaceLimit: 1, timeProvider);
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
        timeProvider.Advance(TimeSpan.FromMinutes(30));

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
        var timeProvider = new FakeTimeProvider();
        var (services, admin, logs) = await BuildEntitledWithLimitContainerAsync(spaceLimit: 1, timeProvider);
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
        timeProvider.Advance(TimeSpan.FromMinutes(30));

        var undeleteResult = await admin.UndeleteAsync(first.Id!, Ct);

        undeleteResult.IsSuccess.ShouldBeTrue();
        HasQuantizedOverLimitRecord(logs, startIndex).ShouldBeTrue();
    }

    [Fact]
    public async Task validate_count_swallows_count_delegate_exception()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterEntitledWithSpaceLimit(services, spaceLimit: 1, out _);
        await using var serviceProvider = services.BuildServiceProvider();

        var validator = serviceProvider.GetRequiredService<SpacesLicenseValidator>();

        var act = async () => await validator.ValidateSpaceCountAsync(
            _ => throw new InvalidOperationException("boom"),
            Ct.None);

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task when_a_space_is_used_30_minutes_after_spaces_were_created_without_a_license_expect_the_warning_to_show_the_current_space_count()
    {
        var timeProvider = new FakeTimeProvider();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.AddFakeLogging();
        builder.Services.AddSingleton<TimeProvider>(timeProvider);
        builder.Services.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterUnlicensed(builder.Services);
        builder.Services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        app.UseSpaceResolution();
        app.UseRouting();
        app.MapGet("/ping", () => Results.Ok());
        await app.StartAsync(Ct);
        var admin = app.Services.GetRequiredService<ISpaceAdmin>();
        foreach (var spaceName in new[] { "space-a", "space-b", "space-c" })
        {
            var created = await admin.CreateAsync(
                new CreateSpaceConfiguration { Name = spaceName, MatchPatterns = [new SpaceMatchPattern { Path = $"/{spaceName}" }] },
                Ct);
            created.IsSuccess.ShouldBeTrue();
        }

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        timeProvider.Advance(TimeSpan.FromMinutes(30));
        var response = await client.GetAsync("/t/space-c/ping", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lastSpaceCountWarning = app.Services.GetFakeLogCollector().GetSnapshot()
            .Last(record => record.Message.StartsWith("Space Count", StringComparison.Ordinal));
        lastSpaceCountWarning.Message.ShouldBe("Space Count detected (3 registered) but no Duende license is configured. Please start a conversation with us: https://duende.link/l/contact");
    }

    [Fact]
    public async Task when_the_space_count_is_checked_again_within_30_minutes_expect_no_new_count_until_the_window_ends()
    {
        var timeProvider = new FakeTimeProvider();
        var (services, validator, _) = BuildUnlicensedValidator(timeProvider);
        await using var _ = services;
        var countsRun = 0;
        Task<long> CountSpaces(Ct ct)
        {
            countsRun++;
            return Task.FromResult(1L);
        }

        await validator.ValidateSpaceCountAsync(CountSpaces, Ct);
        timeProvider.Advance(TimeSpan.FromMinutes(29));
        await validator.ValidateSpaceCountAsync(CountSpaces, Ct);
        var countsRunWithinWindow = countsRun;
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await validator.ValidateSpaceCountAsync(CountSpaces, Ct);

        countsRunWithinWindow.ShouldBe(1);
        countsRun.ShouldBe(2);
    }

    [Fact]
    public async Task when_a_count_finishes_faster_than_the_previous_count_without_a_license_expect_the_next_check_after_the_window_to_log_the_new_count()
    {
        var timeProvider = new FakeTimeProvider();
        var (services, validator, logs) = BuildUnlicensedValidator(timeProvider);
        await using var _ = services;
        var start = timeProvider.GetUtcNow();

        await validator.ValidateSpaceCountAsync(
            ct =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return Task.FromResult(1L);
            },
            Ct);
        Task<long> FastCountOfTwoSpaces(Ct ct)
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
            return Task.FromResult(2L);
        }

        timeProvider.Advance(start.AddMinutes(30) - timeProvider.GetUtcNow());
        await validator.ValidateSpaceCountAsync(FastCountOfTwoSpaces, Ct);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await validator.ValidateSpaceCountAsync(FastCountOfTwoSpaces, Ct);

        var lastSpaceCountWarning = logs.GetSnapshot()
            .Last(record => record.Message.StartsWith("Space Count", StringComparison.Ordinal));
        lastSpaceCountWarning.Message.ShouldStartWith("Space Count detected (2 registered)");
    }

    [Fact]
    public async Task when_a_space_count_does_not_finish_within_5_seconds_expect_the_check_to_stop_waiting()
    {
        var timeProvider = new FakeTimeProvider();
        var (services, validator, _) = BuildUnlicensedValidator(timeProvider);
        await using var _ = services;
        static async Task<long> CountThatNeverFinishes(Ct ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return 1L;
        }

        var check = validator.ValidateSpaceCountAsync(CountThatNeverFinishes, Ct);
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        var finished = await Task.WhenAny(check, Task.Delay(TimeSpan.FromSeconds(10), Ct)) == check;

        finished.ShouldBeTrue();
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
        services.AddIdentityServerBuilder().AddSpaces(_ => { });
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
        BuildEntitledWithLimitContainerAsync(int spaceLimit, TimeProvider timeProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(timeProvider);
        services.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterEntitledWithSpaceLimit(services, spaceLimit, out var logs);
        services.AddStorageInternal(b => b.AddSqliteInMemory());
        var serviceProvider = services.BuildServiceProvider();

        var storageInstanceSchema = serviceProvider.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(Ct);

        var admin = serviceProvider.GetRequiredService<ISpaceAdmin>();
        return (serviceProvider, admin, logs);
    }

    private static (ServiceProvider Services, SpacesLicenseValidator Validator, FakeLogCollector Logs)
        BuildUnlicensedValidator(TimeProvider timeProvider)
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddSingleton(timeProvider);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterUnlicensed(services);
        var serviceProvider = services.BuildServiceProvider();
        return (serviceProvider, serviceProvider.GetRequiredService<SpacesLicenseValidator>(), serviceProvider.GetFakeLogCollector());
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
