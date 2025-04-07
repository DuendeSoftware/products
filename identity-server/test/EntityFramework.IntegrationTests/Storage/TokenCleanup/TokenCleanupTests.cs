// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.EntityFramework;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IPersistedGrantStore = Duende.IdentityServer.Stores.IPersistedGrantStore;

namespace EntityFramework.IntegrationTests.Storage.TokenCleanup;

public class TokenCleanupTests : IntegrationTest<TokenCleanupTests, PersistedGrantDbContext, OperationalStoreOptions>
{
    public TokenCleanupTests(DatabaseProviderFixture<PersistedGrantDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using (var context = new PersistedGrantDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            // The db context is only created once, so before each test
            // we destroy any persisted grants that are left-over
            using (var context = new PersistedGrantDbContext(options))
            {
                var existing = context.PersistedGrants.ToArray();
                context.PersistedGrants.RemoveRange(existing);
                context.SaveChanges();
            }
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenExpiredGrantsExist_ExpectExpiredGrantsRemoved(DbContextOptions<PersistedGrantDbContext> options)
    {
        var expiredGrant = new PersistedGrant
        {
            Key = Guid.NewGuid().ToString(),
            ClientId = "app1",
            Type = "reference",
            SubjectId = "123",
            Expiration = DateTime.UtcNow.AddDays(-3),
            Data = "{!}"
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.Add(expiredGrant);
            await context.SaveChangesAsync();
        }

        await CreateSut(options).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.FirstOrDefault(x => x.Key == expiredGrant.Key).ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenValidGrantsExist_ExpectValidGrantsInDb(DbContextOptions<PersistedGrantDbContext> options)
    {
        var validGrant = new PersistedGrant
        {
            Key = Guid.NewGuid().ToString(),
            ClientId = "app1",
            Type = "reference",
            SubjectId = "123",
            Expiration = DateTime.UtcNow.AddDays(3),
            Data = "{!}"
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.Add(validGrant);
            await context.SaveChangesAsync();
        }

        await CreateSut(options).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.FirstOrDefault(x => x.Key == validGrant.Key).ShouldNotBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenBothExpiredAndValidGrantsExists_ExpectOnlyExpiredGrantsRemoved(DbContextOptions<PersistedGrantDbContext> options)
    {
        StoreOptions.TokenCleanupBatchSize = 20;

        var expiredGrants = Enumerable.Range(1, 45)
            .Select(i =>
                new PersistedGrant
                {
                    Key = "expired-" + i,
                    ClientId = "app1",
                    Type = "reference",
                    SubjectId = "123",
                    Expiration = DateTime.UtcNow.AddMinutes(-i),
                    Data = "{!}"
                });

        var validGrants = Enumerable.Range(1, 15)
            .Select(i =>
                new PersistedGrant
                {
                    Key = "valid-" + i,
                    ClientId = "app1",
                    Type = "reference",
                    SubjectId = "123",
                    Expiration = DateTime.UtcNow.AddMinutes(i),
                    Data = "{!}"
                });

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.AddRange(expiredGrants);
            context.PersistedGrants.AddRange(validGrants);
            await context.SaveChangesAsync();
        }

        await CreateSut(options).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            var remaining = context.PersistedGrants.ToList();

            remaining.Count.ShouldBe(15);
            remaining.All(r => r.Key.StartsWith("valid-")).ShouldBeTrue();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenExpiredDeviceGrantsExist_ExpectExpiredDeviceGrantsRemoved(DbContextOptions<PersistedGrantDbContext> options)
    {
        var expiredGrant = new DeviceFlowCodes
        {
            DeviceCode = Guid.NewGuid().ToString(),
            UserCode = Guid.NewGuid().ToString(),
            ClientId = "app1",
            SubjectId = "123",
            CreationTime = DateTime.UtcNow.AddDays(-4),
            Expiration = DateTime.UtcNow.AddDays(-3),
            Data = "{!}"
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(expiredGrant);
            await context.SaveChangesAsync();
        }

        await CreateSut(options).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.FirstOrDefault(x => x.DeviceCode == expiredGrant.DeviceCode).ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenValidDeviceGrantsExist_ExpectValidDeviceGrantsInDb(DbContextOptions<PersistedGrantDbContext> options)
    {
        var validGrant = new DeviceFlowCodes
        {
            DeviceCode = Guid.NewGuid().ToString(),
            UserCode = "2468",
            ClientId = "app1",
            SubjectId = "123",
            CreationTime = DateTime.UtcNow.AddDays(-4),
            Expiration = DateTime.UtcNow.AddDays(3),
            Data = "{!}"
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(validGrant);
            await context.SaveChangesAsync();
        }

        await CreateSut(options).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.FirstOrDefault(x => x.DeviceCode == validGrant.DeviceCode).ShouldNotBeNull();
        }
    }


    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenFlagIsOnAndConsumedGrantsExist_ExpectConsumedGrantsRemoved(DbContextOptions<PersistedGrantDbContext> options)
    {
        var consumedGrant = new PersistedGrant
        {
            Expiration = DateTime.UtcNow.AddDays(3), // Token not yet expired
            ConsumedTime = DateTime.UtcNow.AddMinutes(-15), // But was consumed

            Key = Guid.NewGuid().ToString(),
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
            ClientId = "app1",
            Data = "{!}"
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.Add(consumedGrant);
            await context.SaveChangesAsync();
        }

        await CreateSut(options, removeConsumedTokens: true).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.FirstOrDefault(x => x.Id == consumedGrant.Id).ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenFlagIsOffAndConsumedGrantsExist_ExpectConsumedGrantsNotRemoved(DbContextOptions<PersistedGrantDbContext> options)
    {
        var consumedGrant = new PersistedGrant
        {
            Expiration = DateTime.UtcNow.AddDays(3), // Token not yet expired
            ConsumedTime = DateTime.UtcNow.AddMinutes(-15), // But was consumed

            Key = Guid.NewGuid().ToString(),
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
            ClientId = "app1",
            Data = "{!}"
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.Add(consumedGrant);
            await context.SaveChangesAsync();
        }

        await CreateSut(options, removeConsumedTokens: false).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.FirstOrDefault(x => x.Id == consumedGrant.Id).ShouldNotBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_ExpectBatchSizeIsRespected(DbContextOptions<PersistedGrantDbContext> options)
    {
        StoreOptions.TokenCleanupBatchSize = 20;

        var expectedPageCount = 5;

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.ToList().ShouldBeEmpty();

            for (var i = 0; i < StoreOptions.TokenCleanupBatchSize * expectedPageCount; i++)
            {
                var expiredGrant = new PersistedGrant
                {
                    Expiration = DateTime.UtcNow.AddMinutes(-1),

                    Key = Guid.NewGuid().ToString(),
                    Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
                    ClientId = "app1",
                    Data = "{!}"
                };
                context.PersistedGrants.Add(expiredGrant);
            }

            await context.SaveChangesAsync();

            context.PersistedGrants.Count().ShouldBe(StoreOptions.TokenCleanupBatchSize * expectedPageCount);
        }

        var mockNotifications = new MockOperationalStoreNotification();

        await CreateSut(options, svcs =>
        {
            svcs.AddSingleton<IOperationalStoreNotification>(mockNotifications);
        }).CleanupGrantsAsync();

        // The right number of batches executed
        mockNotifications.PersistedGrantNotifications.Count.ShouldBe(expectedPageCount);

        // Each batch contained the expected number of grants
        foreach (var notification in mockNotifications.PersistedGrantNotifications)
        {
            notification.Count().ShouldBe(StoreOptions.TokenCleanupBatchSize);
        }

        // All grants are removed because they were all expired
        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.ToList().ShouldBeEmpty();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_InsertsBetweenBatches_ExpectAdditionalBatches(DbContextOptions<PersistedGrantDbContext> options)
    {
        StoreOptions.TokenCleanupBatchSize = 20;

        var expectedPageCount = 5;

        await using (var context = new PersistedGrantDbContext(options))
        {
            for (var i = 0; i < StoreOptions.TokenCleanupBatchSize * expectedPageCount; i++)
            {
                var expiredGrant = new PersistedGrant
                {
                    Expiration = DateTime.UtcNow.AddMinutes(-1),

                    Key = Guid.NewGuid().ToString(),
                    Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
                    ClientId = "app1",
                    Data = "{!}"
                };
                context.PersistedGrants.Add(expiredGrant);
            }

            await context.SaveChangesAsync();
        }

        // Whenever we cleanup a batch of grants, a new (expired) grant is inserted
        var mockNotifications = new MockOperationalStoreNotification()
        {
            OnPersistedGrantsRemoved = grants =>
            {
                using (var context = new PersistedGrantDbContext(options))
                {
                    var expiredGrant = new PersistedGrant
                    {
                        Expiration = DateTime.UtcNow.AddMinutes(-1),

                        Key = Guid.NewGuid().ToString(),
                        Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
                        ClientId = "app1",
                        Data = "{Extra grant created between pages}"
                    };
                    context.PersistedGrants.Add(expiredGrant);
                    context.SaveChanges();
                }
            }
        };

        await CreateSut(options, svcs =>
        {
            svcs.AddSingleton<IOperationalStoreNotification>(mockNotifications);
        }).CleanupGrantsAsync();

        // Each batch created an extra grant, so we do an extra batch to clean up
        // the extras
        mockNotifications.PersistedGrantNotifications.Count.ShouldBe(expectedPageCount + 1);

        // Each batch had the expected number of grants. Most batches had the batch size grants
        for (var i = 0; i < expectedPageCount; i++)
        {
            mockNotifications.PersistedGrantNotifications[i].Count().ShouldBe(StoreOptions.TokenCleanupBatchSize);
        }

        // The last batch had the extras - there is one extra per page
        mockNotifications.PersistedGrantNotifications.Last().Count().ShouldBe(expectedPageCount);

        // In the end, all but one get deleted
        // One final grant will be left behind, created by the last notification to fire
        // We can treat this as the first grant created after the job ran, 
        // we just are able to observe it because it was created in the final batch's notification
        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.Count().ShouldBe(1);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CleanupGrantsAsync_WhenFlagIsOnAndConsumedGrantsExistAndDelayIsSet_ExpectConsumedGrantsRemovedRespectsDelay(DbContextOptions<PersistedGrantDbContext> options)
    {
        var delay = 100;

        // This grant was consumed long enough in the past that it should be deleted
        var oldConsumedGrant = new PersistedGrant
        {
            Expiration = DateTime.UtcNow.AddDays(3),                    // Token not yet expired
            ConsumedTime = DateTime.UtcNow.AddSeconds(-(delay + 100)),  // But was consumed MORE than the delay in the past

            Key = Guid.NewGuid().ToString(),
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
            ClientId = "app1",
            Data = "{!}"
        };

        // This grant was consumed recently enough that it should not be deleted
        var newConsumedGrant = new PersistedGrant
        {
            Expiration = DateTime.UtcNow.AddDays(3),                    // Token not yet expired
            ConsumedTime = DateTime.UtcNow.AddSeconds(-(delay - 100)),  // But was consumed LESS than the delay in the past

            Key = Guid.NewGuid().ToString(),
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
            ClientId = "app1",
            Data = "{!}"
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.Add(newConsumedGrant);
            context.PersistedGrants.Add(oldConsumedGrant);
            await context.SaveChangesAsync();
        }

        await CreateSut(options, removeConsumedTokens: true, delay).CleanupGrantsAsync();

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.PersistedGrants.FirstOrDefault(x => x.Id == newConsumedGrant.Id).ShouldNotBeNull();
            context.PersistedGrants.FirstOrDefault(x => x.Id == oldConsumedGrant.Id).ShouldBeNull();
        }
    }

    private TokenCleanupService CreateSut(
        DbContextOptions<PersistedGrantDbContext> dbContextOpts,
        bool removeConsumedTokens,
        int consumedTokenCleanupDelay = 0
    )
    {
        StoreOptions.RemoveConsumedTokens = removeConsumedTokens;
        StoreOptions.ConsumedTokenCleanupDelay = consumedTokenCleanupDelay;
        return CreateSut(dbContextOpts);
    }

    private TokenCleanupService CreateSut(
        DbContextOptions<PersistedGrantDbContext> options,
        Action<IServiceCollection> configureServices
    )
    {
        IServiceCollection services = new ServiceCollection();

        configureServices(services);

        services.AddIdentityServer()
            .AddTestUsers(new List<TestUser>())
            .AddInMemoryClients(new List<Duende.IdentityServer.Models.Client>())
            .AddInMemoryIdentityResources(new List<Duende.IdentityServer.Models.IdentityResource>())
            .AddInMemoryApiResources(new List<Duende.IdentityServer.Models.ApiResource>());

        services.AddScoped<IPersistedGrantDbContext, PersistedGrantDbContext>(_ =>
            new PersistedGrantDbContext(options));
        services.AddTransient<IPersistedGrantStore, PersistedGrantStore>();
        services.AddTransient<IDeviceFlowStore, DeviceFlowStore>();

        services.AddTransient<ITokenCleanupService, TokenCleanupService>();
        services.AddSingleton(StoreOptions);

        return services.BuildServiceProvider().GetRequiredService<ITokenCleanupService>() as TokenCleanupService;
    }

    private TokenCleanupService CreateSut(DbContextOptions<PersistedGrantDbContext> options)
    {
        return CreateSut(options, _ => { });
    }
}
