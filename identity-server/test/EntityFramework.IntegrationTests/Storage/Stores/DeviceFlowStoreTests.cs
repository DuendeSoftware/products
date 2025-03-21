// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace EntityFramework.IntegrationTests.Storage.Stores;

public class DeviceFlowStoreTests : IntegrationTest<DeviceFlowStoreTests, PersistedGrantDbContext, OperationalStoreOptions>
{
    private readonly IPersistentGrantSerializer serializer = new PersistentGrantSerializer();

    public DeviceFlowStoreTests(DatabaseProviderFixture<PersistedGrantDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new PersistedGrantDbContext(options);
            context.Database.EnsureCreated();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreDeviceAuthorizationAsync_WhenSuccessful_ExpectDeviceCodeAndUserCodeStored(DbContextOptions<PersistedGrantDbContext> options)
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var foundDeviceFlowCodes = context.DeviceFlowCodes.FirstOrDefault(x => x.DeviceCode == deviceCode);

            foundDeviceFlowCodes.ShouldNotBeNull();
            foundDeviceFlowCodes?.DeviceCode.ShouldBe(deviceCode);
            foundDeviceFlowCodes?.UserCode.ShouldBe(userCode);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreDeviceAuthorizationAsync_WhenSuccessful_ExpectDataStored(DbContextOptions<PersistedGrantDbContext> options)
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var foundDeviceFlowCodes = context.DeviceFlowCodes.FirstOrDefault(x => x.DeviceCode == deviceCode);

            foundDeviceFlowCodes.ShouldNotBeNull();
            var deserializedData = new PersistentGrantSerializer().Deserialize<DeviceCode>(foundDeviceFlowCodes?.Data);

            deserializedData.CreationTime.ShouldBeCloseTo(data.CreationTime, TimeSpan.FromMicroseconds(1));
            deserializedData.ClientId.ShouldBe(data.ClientId);
            deserializedData.Lifetime.ShouldBe(data.Lifetime);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreDeviceAuthorizationAsync_WhenUserCodeAlreadyExists_ExpectException(DbContextOptions<PersistedGrantDbContext> options)
    {
        var existingUserCode = $"user_{Guid.NewGuid().ToString()}";
        var deviceCodeData = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                new List<Claim> { new Claim(JwtClaimTypes.Subject, $"sub_{Guid.NewGuid().ToString()}") }))
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = $"device_{Guid.NewGuid().ToString()}",
                UserCode = existingUserCode,
                ClientId = deviceCodeData.ClientId,
                SubjectId = deviceCodeData.Subject.FindFirst(JwtClaimTypes.Subject).Value,
                CreationTime = deviceCodeData.CreationTime,
                Expiration = deviceCodeData.CreationTime.AddSeconds(deviceCodeData.Lifetime),
                Data = serializer.Serialize(deviceCodeData)
            });
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());

            // skip odd behaviour of in-memory provider
#pragma warning disable EF1001 // Internal EF Core API usage.
            if (options.Extensions.All(x => x.GetType() != typeof(InMemoryOptionsExtension)))
            {
                var act = () => store.StoreDeviceAuthorizationAsync($"device_{Guid.NewGuid().ToString()}", existingUserCode, deviceCodeData);
                await act.ShouldThrowAsync<DbUpdateException>();
            }
#pragma warning restore EF1001 // Internal EF Core API usage.
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task StoreDeviceAuthorizationAsync_WhenDeviceCodeAlreadyExists_ExpectException(DbContextOptions<PersistedGrantDbContext> options)
    {
        var existingDeviceCode = $"device_{Guid.NewGuid().ToString()}";
        var deviceCodeData = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                new List<Claim> { new Claim(JwtClaimTypes.Subject, $"sub_{Guid.NewGuid().ToString()}") }))
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = existingDeviceCode,
                UserCode = $"user_{Guid.NewGuid().ToString()}",
                ClientId = deviceCodeData.ClientId,
                SubjectId = deviceCodeData.Subject.FindFirst(JwtClaimTypes.Subject).Value,
                CreationTime = deviceCodeData.CreationTime,
                Expiration = deviceCodeData.CreationTime.AddSeconds(deviceCodeData.Lifetime),
                Data = serializer.Serialize(deviceCodeData)
            });
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());

            // skip odd behaviour of in-memory provider
#pragma warning disable EF1001 // Internal EF Core API usage.
            if (options.Extensions.All(x => x.GetType() != typeof(InMemoryOptionsExtension)))
            {
                var act = () => store.StoreDeviceAuthorizationAsync(existingDeviceCode, $"user_{Guid.NewGuid().ToString()}", deviceCodeData);
                await act.ShouldThrowAsync<DbUpdateException>();
            }
#pragma warning restore EF1001 // Internal EF Core API usage.
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByUserCodeAsync_WhenUserCodeExists_ExpectDataRetrievedCorrectly(DbContextOptions<PersistedGrantDbContext> options)
    {
        var testDeviceCode = $"device_{Guid.NewGuid().ToString()}";
        var testUserCode = $"user_{Guid.NewGuid().ToString()}";

        var expectedSubject = $"sub_{Guid.NewGuid().ToString()}";
        var expectedDeviceCodeData = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(JwtClaimTypes.Subject, expectedSubject) }))
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = testDeviceCode,
                UserCode = testUserCode,
                ClientId = expectedDeviceCodeData.ClientId,
                SubjectId = expectedDeviceCodeData.Subject.FindFirst(JwtClaimTypes.Subject).Value,
                CreationTime = expectedDeviceCodeData.CreationTime,
                Expiration = expectedDeviceCodeData.CreationTime.AddSeconds(expectedDeviceCodeData.Lifetime),
                Data = serializer.Serialize(expectedDeviceCodeData)
            });
            await context.SaveChangesAsync();
        }

        DeviceCode code;
        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            code = await store.FindByUserCodeAsync(testUserCode);
        }

        code.ShouldSatisfyAllConditions(c =>
        {
            c.ClientId.ShouldBe(expectedDeviceCodeData.ClientId);
            c.RequestedScopes.ShouldBe(expectedDeviceCodeData.RequestedScopes);
            c.CreationTime.ShouldBe(expectedDeviceCodeData.CreationTime);
            c.Lifetime.ShouldBe(expectedDeviceCodeData.Lifetime);
            c.IsOpenId.ShouldBe(expectedDeviceCodeData.IsOpenId);
        });

        code.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByUserCodeAsync_WhenUserCodeDoesNotExist_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            var code = await store.FindByUserCodeAsync($"user_{Guid.NewGuid().ToString()}");
            code.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByDeviceCodeAsync_WhenDeviceCodeExists_ExpectDataRetrievedCorrectly(DbContextOptions<PersistedGrantDbContext> options)
    {
        var testDeviceCode = $"device_{Guid.NewGuid().ToString()}";
        var testUserCode = $"user_{Guid.NewGuid().ToString()}";

        var expectedSubject = $"sub_{Guid.NewGuid().ToString()}";
        var expectedDeviceCodeData = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(JwtClaimTypes.Subject, expectedSubject) }))
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = testDeviceCode,
                UserCode = testUserCode,
                ClientId = expectedDeviceCodeData.ClientId,
                SubjectId = expectedDeviceCodeData.Subject.FindFirst(JwtClaimTypes.Subject).Value,
                CreationTime = expectedDeviceCodeData.CreationTime,
                Expiration = expectedDeviceCodeData.CreationTime.AddSeconds(expectedDeviceCodeData.Lifetime),
                Data = serializer.Serialize(expectedDeviceCodeData)
            });
            await context.SaveChangesAsync();
        }

        DeviceCode code;
        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            code = await store.FindByDeviceCodeAsync(testDeviceCode);
        }

        code.ShouldSatisfyAllConditions(c =>
        {
            c.ClientId.ShouldBe(expectedDeviceCodeData.ClientId);
            c.CreationTime.ShouldBe(expectedDeviceCodeData.CreationTime);
            c.Lifetime.ShouldBe(expectedDeviceCodeData.Lifetime);
            c.IsOpenId.ShouldBe(expectedDeviceCodeData.IsOpenId);
        });

        code.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByDeviceCodeAsync_WhenDeviceCodeDoesNotExist_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            var code = await store.FindByDeviceCodeAsync($"device_{Guid.NewGuid().ToString()}");
            code.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task UpdateByUserCodeAsync_WhenDeviceCodeAuthorized_ExpectSubjectAndDataUpdated(DbContextOptions<PersistedGrantDbContext> options)
    {
        var testDeviceCode = $"device_{Guid.NewGuid().ToString()}";
        var testUserCode = $"user_{Guid.NewGuid().ToString()}";

        var expectedSubject = $"sub_{Guid.NewGuid().ToString()}";
        var unauthorizedDeviceCode = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300,
            IsOpenId = true
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = testDeviceCode,
                UserCode = testUserCode,
                ClientId = unauthorizedDeviceCode.ClientId,
                CreationTime = unauthorizedDeviceCode.CreationTime,
                Expiration = unauthorizedDeviceCode.CreationTime.AddSeconds(unauthorizedDeviceCode.Lifetime),
                Data = serializer.Serialize(unauthorizedDeviceCode)
            });
            await context.SaveChangesAsync();
        }

        var authorizedDeviceCode = new DeviceCode
        {
            ClientId = unauthorizedDeviceCode.ClientId,
            RequestedScopes = unauthorizedDeviceCode.RequestedScopes,
            AuthorizedScopes = unauthorizedDeviceCode.RequestedScopes,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(JwtClaimTypes.Subject, expectedSubject) })),
            IsAuthorized = true,
            IsOpenId = true,
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.UpdateByUserCodeAsync(testUserCode, authorizedDeviceCode);
        }

        DeviceFlowCodes updatedCodes;
        await using (var context = new PersistedGrantDbContext(options))
        {
            updatedCodes = context.DeviceFlowCodes.Single(x => x.UserCode == testUserCode);
        }

        // should be unchanged
        updatedCodes.DeviceCode.ShouldBe(testDeviceCode);
        updatedCodes.ClientId.ShouldBe(unauthorizedDeviceCode.ClientId);
        updatedCodes.CreationTime.ShouldBe(unauthorizedDeviceCode.CreationTime);
        updatedCodes.Expiration.ShouldBe(unauthorizedDeviceCode.CreationTime.AddSeconds(authorizedDeviceCode.Lifetime));

        // should be changed
        var parsedCode = serializer.Deserialize<DeviceCode>(updatedCodes.Data);
        parsedCode.ShouldSatisfyAllConditions(c =>
        {
            c.ClientId.ShouldBe(authorizedDeviceCode.ClientId);
            c.RequestedScopes.ShouldBe(authorizedDeviceCode.RequestedScopes);
            c.AuthorizedScopes = authorizedDeviceCode.AuthorizedScopes;
            c.IsAuthorized.ShouldBe(authorizedDeviceCode.IsAuthorized);
            c.IsOpenId.ShouldBe(authorizedDeviceCode.IsOpenId);
            c.CreationTime.ShouldBe(authorizedDeviceCode.CreationTime);
            c.Lifetime.ShouldBe(authorizedDeviceCode.Lifetime);

        });
        parsedCode.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveByDeviceCodeAsync_WhenDeviceCodeExists_ExpectDeviceCodeDeleted(DbContextOptions<PersistedGrantDbContext> options)
    {
        var testDeviceCode = $"device_{Guid.NewGuid().ToString()}";
        var testUserCode = $"user_{Guid.NewGuid().ToString()}";

        var existingDeviceCode = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300,
            IsOpenId = true
        };

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = testDeviceCode,
                UserCode = testUserCode,
                ClientId = existingDeviceCode.ClientId,
                CreationTime = existingDeviceCode.CreationTime,
                Expiration = existingDeviceCode.CreationTime.AddSeconds(existingDeviceCode.Lifetime),
                Data = serializer.Serialize(existingDeviceCode)
            });
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.RemoveByDeviceCodeAsync(testDeviceCode);
        }

        await using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.FirstOrDefault(x => x.UserCode == testUserCode).ShouldBeNull();
        }
    }
    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveByDeviceCodeAsync_WhenDeviceCodeDoesNotExists_ExpectSuccess(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.RemoveByDeviceCodeAsync($"device_{Guid.NewGuid().ToString()}");
        }
    }
}
