// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Spaces;
using Duende.Spaces.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer8.UnitTests.Licensing;

public sealed class SpaceAwareUserCountProviderTests : IAsyncDisposable
{
    private static Ct Ct => TestContext.Current.CancellationToken;

    private ServiceProvider? _services;

    // Disposing the provider disposes the license validator, which cancels and drains any
    // count check started by the users these tests create.
    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetUserCountAsync_sums_default_and_enabled_spaces()
    {
        var sp = await BuildAsync(withSpaces: true);
        await CreateUsersAsync(sp, SpaceId.Default, 2);
        await CreateUsersAsync(sp, await CreateSpaceAsync(sp, "tenant-a"), 3);
        await CreateUsersAsync(sp, await CreateSpaceAsync(sp, "tenant-b"), 4);

        var total = await CountAsync(sp);

        total.ShouldBe(9);
    }

    [Fact]
    public async Task GetUserCountAsync_excludes_disabled_deleted_and_management_spaces()
    {
        var sp = await BuildAsync(withSpaces: true);
        await CreateUsersAsync(sp, SpaceId.Default, 2);
        await CreateUsersAsync(sp, await CreateSpaceAsync(sp, "enabled"), 3);
        var disabled = await CreateSpaceAsync(sp, "disabled");
        await CreateUsersAsync(sp, disabled, 5);
        await DisableSpaceAsync(sp, disabled);
        var deleted = await CreateSpaceAsync(sp, "deleted");
        await CreateUsersAsync(sp, deleted, 6);
        (await sp.GetRequiredService<ISpaceAdmin>().DeleteAsync(deleted, Ct)).IsSuccess.ShouldBeTrue();
        await CreateUsersAsync(sp, SpaceId.Management, 7);

        var total = await CountAsync(sp);

        total.ShouldBe(5);
    }

    [Fact]
    public async Task GetUserCountAsync_counts_enabled_spaces_across_multiple_pages()
    {
        const int spaceCount = 30;
        var sp = await BuildAsync(withSpaces: true);
        await CreateUsersAsync(sp, SpaceId.Default, 1);
        var expected = 1;
        for (var i = 0; i < spaceCount; i++)
        {
            var spaceId = await CreateSpaceAsync(sp, $"tenant-{i:D2}");
            await CreateUsersAsync(sp, spaceId, 1);
            if (i % 10 == 9)
            {
                await DisableSpaceAsync(sp, spaceId);
            }
            else
            {
                expected++;
            }
        }

        var total = await CountAsync(sp);

        total.ShouldBe(expected);
    }

    [Fact]
    public async Task GetUserCountAsync_is_independent_of_ambient_space()
    {
        var sp = await BuildAsync(withSpaces: true);
        await CreateUsersAsync(sp, SpaceId.Default, 2);
        var tenant = await CreateSpaceAsync(sp, "tenant");
        await CreateUsersAsync(sp, tenant, 3);

        long fromTenant;
        using (sp.GetRequiredService<ISpaceContextAccessor>().SetSpace(tenant))
        {
            fromTenant = await CountAsync(sp);
        }
        var withoutContext = await CountAsync(sp);

        fromTenant.ShouldBe(5);
        withoutContext.ShouldBe(5);
    }

    [Fact]
    public async Task GetUserCountAsync_without_Spaces_counts_current_store()
    {
        var sp = await BuildAsync(withSpaces: false);
        await CreateUsersAsync(sp, 3);

        var total = await CountAsync(sp);

        total.ShouldBe(3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetUserCountAsync_with_incomplete_Spaces_registration_logs_failure_and_skips_validation(
        bool missingSpaceAdmin)
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        FakeLogCollector licenseLogs = null!;
        var sp = await BuildAsync(withSpaces: true, services =>
        {
            _ = services.RemoveAll<TimeProvider>();
            _ = services.AddSingleton<TimeProvider>(time);
            licenseLogs = TestUserManagementLicense.Create().WithUserLimit(0).Register(services, time);
            if (missingSpaceAdmin)
            {
                _ = services.RemoveAll<ISpaceAdmin>();
            }
            else
            {
                _ = services.RemoveAll<ISpaceContextAccessor>();
            }
        });

        _ = await Should.ThrowAsync<InvalidOperationException>(() => CountAsync(sp));
        var validator = sp.GetRequiredService<UserManagementLicenseValidator>();
        await validator.TryStartUserCountCheck().ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(30), Ct);

        var records = sp.GetRequiredService<FakeLogCollector>().GetSnapshot();
        var warning = records
            .Where(r => r.Level == LogLevel.Warning && r.Id.Name == "UserCountCheckFailed")
            .ShouldHaveSingleItem();
        _ = warning.Exception.ShouldBeOfType<InvalidOperationException>();
        records.ShouldNotContain(r => r.Id.Name == "UserCountCheckCompleted");
        licenseLogs.GetSnapshot().ShouldBeEmpty();
    }

    private Task<ServiceProvider> BuildAsync(bool withSpaces) => BuildAsync(withSpaces, _ => { });

    private async Task<ServiceProvider> BuildAsync(bool withSpaces, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        _ = services.AddFakeLogging();
        _ = services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug));
        var builder = services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage();
        _ = builder.AddUserManagement(_ => { });
        if (withSpaces)
        {
            _ = services.AddSpacesInternal();
        }

        configure(services);
        var sp = _services = services.BuildServiceProvider();
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        return sp;
    }

    private static async Task<long> CountAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserCountProvider>().GetUserCountAsync(Ct);
    }

    private static async Task<SpaceId> CreateSpaceAsync(IServiceProvider sp, string name)
    {
        var created = await sp.GetRequiredService<ISpaceAdmin>().CreateAsync(new CreateSpaceConfiguration
        {
            Name = name,
            MatchPatterns = [new SpaceMatchPattern { Path = $"/{name}" }]
        }, Ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
        return created.Id;
    }

    private static async Task DisableSpaceAsync(IServiceProvider sp, SpaceId spaceId)
    {
        var spaceAdmin = sp.GetRequiredService<ISpaceAdmin>();
        var current = await spaceAdmin.GetAsync(spaceId, Ct);
        current.Found.ShouldBeTrue();
        current.Item!.Enabled = false;
        var updated = await spaceAdmin.UpdateAsync(spaceId, current.Item, current.Version!, Ct);
        updated.IsSuccess.ShouldBeTrue($"Update failed: {updated}");
    }

    private static async Task CreateUsersAsync(IServiceProvider sp, SpaceId spaceId, int count)
    {
        using var space = sp.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceId);
        await CreateUsersAsync(sp, count);
    }

    private static async Task CreateUsersAsync(IServiceProvider sp, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await using var scope = sp.CreateAsyncScope();
            var address = new ExternalAuthenticatorAddress(
                ExternalAuthenticatorName.Create("test-ext"),
                OpaqueSubjectId.Create(Guid.NewGuid().ToString()));
            var result = await scope.ServiceProvider.GetRequiredService<IExternalAuthenticator>().TryAuthenticateAsync(address, Ct);
            _ = result.ShouldBeOfType<ExternalAuthenticationResult.Success>();
        }
    }
}
