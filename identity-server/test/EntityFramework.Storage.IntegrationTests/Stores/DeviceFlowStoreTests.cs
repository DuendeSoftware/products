// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Serialization;
using FluentAssertions;
using Duende.IdentityModel;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Xunit;
using Duende.IdentityServer.Services;

namespace EntityFramework.Storage.IntegrationTests.Stores;

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

        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data);
        }

        using (var context = new PersistedGrantDbContext(options))
        {
            var foundDeviceFlowCodes = context.DeviceFlowCodes.FirstOrDefault(x => x.DeviceCode == deviceCode);

            foundDeviceFlowCodes.Should().NotBeNull();
            foundDeviceFlowCodes?.DeviceCode.Should().Be(deviceCode);
            foundDeviceFlowCodes?.UserCode.Should().Be(userCode);
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

        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data);
        }

        using (var context = new PersistedGrantDbContext(options))
        {
            var foundDeviceFlowCodes = context.DeviceFlowCodes.FirstOrDefault(x => x.DeviceCode == deviceCode);

            foundDeviceFlowCodes.Should().NotBeNull();
            var deserializedData = new PersistentGrantSerializer().Deserialize<DeviceCode>(foundDeviceFlowCodes?.Data);

            deserializedData.CreationTime.Should().BeCloseTo(data.CreationTime, TimeSpan.FromMicroseconds(1));
            deserializedData.ClientId.Should().Be(data.ClientId);
            deserializedData.Lifetime.Should().Be(data.Lifetime);
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

        using (var context = new PersistedGrantDbContext(options))
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
            context.SaveChanges();
        }

        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());

            // skip odd behaviour of in-memory provider
#pragma warning disable EF1001 // Internal EF Core API usage.
            if (options.Extensions.All(x => x.GetType() != typeof(InMemoryOptionsExtension)))
            {
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    store.StoreDeviceAuthorizationAsync($"device_{Guid.NewGuid().ToString()}", existingUserCode, deviceCodeData));
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

        using (var context = new PersistedGrantDbContext(options))
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
            context.SaveChanges();
        }

        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());

            // skip odd behaviour of in-memory provider
#pragma warning disable EF1001 // Internal EF Core API usage.
            if (options.Extensions.All(x => x.GetType() != typeof(InMemoryOptionsExtension)))
            {
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    store.StoreDeviceAuthorizationAsync(existingDeviceCode, $"user_{Guid.NewGuid().ToString()}", deviceCodeData));
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

        using (var context = new PersistedGrantDbContext(options))
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
            context.SaveChanges();
        }

        DeviceCode code;
        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            code = await store.FindByUserCodeAsync(testUserCode);
        }
            
        code.Should().BeEquivalentTo(expectedDeviceCodeData, 
            assertionOptions => assertionOptions.Excluding(x=> x.Subject));

        code.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).Should().NotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByUserCodeAsync_WhenUserCodeDoesNotExist_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            var code = await store.FindByUserCodeAsync($"user_{Guid.NewGuid().ToString()}");
            code.Should().BeNull();
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

        using (var context = new PersistedGrantDbContext(options))
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
            context.SaveChanges();
        }

        DeviceCode code;
        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            code = await store.FindByDeviceCodeAsync(testDeviceCode);
        }

        code.Should().BeEquivalentTo(expectedDeviceCodeData,
            assertionOptions => assertionOptions.Excluding(x => x.Subject));

        code.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).Should().NotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByDeviceCodeAsync_WhenDeviceCodeDoesNotExist_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            var code = await store.FindByDeviceCodeAsync($"device_{Guid.NewGuid().ToString()}");
            code.Should().BeNull();
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
            RequestedScopes = new[] {"openid", "api1"},
            CreationTime = new DateTime(2018, 10, 19, 16, 14, 29),
            Lifetime = 300,
            IsOpenId = true
        };

        using (var context = new PersistedGrantDbContext(options))
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
            context.SaveChanges();
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

        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.UpdateByUserCodeAsync(testUserCode, authorizedDeviceCode);
        }

        DeviceFlowCodes updatedCodes;
        using (var context = new PersistedGrantDbContext(options))
        {
            updatedCodes = context.DeviceFlowCodes.Single(x => x.UserCode == testUserCode);
        }

        // should be unchanged
        updatedCodes.DeviceCode.Should().Be(testDeviceCode);
        updatedCodes.ClientId.Should().Be(unauthorizedDeviceCode.ClientId);
        updatedCodes.CreationTime.Should().Be(unauthorizedDeviceCode.CreationTime);
        updatedCodes.Expiration.Should().Be(unauthorizedDeviceCode.CreationTime.AddSeconds(authorizedDeviceCode.Lifetime));

        // should be changed
        var parsedCode = serializer.Deserialize<DeviceCode>(updatedCodes.Data);
        parsedCode.Should().BeEquivalentTo(authorizedDeviceCode, assertionOptions => assertionOptions.Excluding(x => x.Subject));
        parsedCode.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).Should().NotBeNull();
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

        using (var context = new PersistedGrantDbContext(options))
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
            context.SaveChanges();
        }
            
        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.RemoveByDeviceCodeAsync(testDeviceCode);
        }
            
        using (var context = new PersistedGrantDbContext(options))
        {
            context.DeviceFlowCodes.FirstOrDefault(x => x.UserCode == testUserCode).Should().BeNull();
        }
    }
    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task RemoveByDeviceCodeAsync_WhenDeviceCodeDoesNotExists_ExpectSuccess(DbContextOptions<PersistedGrantDbContext> options)
    {
        using (var context = new PersistedGrantDbContext(options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), FakeLogger<DeviceFlowStore>.Create(), new NoneCancellationTokenProvider());
            await store.RemoveByDeviceCodeAsync($"device_{Guid.NewGuid().ToString()}");
        }
    }
}