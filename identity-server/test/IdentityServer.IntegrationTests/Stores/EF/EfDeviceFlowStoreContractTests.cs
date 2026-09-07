// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.EntityFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Stores.EF;

public class EfDeviceFlowStoreContractTests : DeviceFlowStoreContractTests
{
    private readonly DbContextOptions<PersistedGrantDbContext> _options;
    private readonly IPersistentGrantSerializer _serializer = new PersistentGrantSerializer();

    public EfDeviceFlowStoreContractTests()
    {
        _options = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>(
            $"EfDeviceFlowContract_{Guid.NewGuid():N}", new OperationalStoreOptions());
        using var context = new PersistedGrantDbContext(_options);
        context.Database.EnsureCreated();
    }

    protected override StoreHandle<IDeviceFlowStore> CreateStore()
    {
        var context = new PersistedGrantDbContext(_options);
        IDeviceFlowStore store = new DeviceFlowStore(context, new PersistentGrantSerializer(), new NullLogger<DeviceFlowStore>());
        return StoreHandle.WithAsyncDisposable(store, context);
    }

    [Fact]
    public async Task StoreDeviceAuthorizationAsync_WhenUserCodeAlreadyExists_ExpectException()
    {
        var existingUserCode = $"user_{Guid.NewGuid()}";
        var deviceCodeData = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, $"sub_{Guid.NewGuid()}")]))
        };

        await using (var context = new PersistedGrantDbContext(_options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = $"device_{Guid.NewGuid()}",
                UserCode = existingUserCode,
                ClientId = deviceCodeData.ClientId,
                SubjectId = deviceCodeData.Subject.FindFirst(JwtClaimTypes.Subject)!.Value,
                CreationTime = deviceCodeData.CreationTime,
                Expiration = deviceCodeData.CreationTime.AddSeconds(deviceCodeData.Lifetime),
                Data = _serializer.Serialize(deviceCodeData)
            });
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), new NullLogger<DeviceFlowStore>());

            // SQLite enforces unique constraints, so storing a duplicate user code throws.
            var act = () => store.StoreDeviceAuthorizationAsync($"device_{Guid.NewGuid()}", existingUserCode, deviceCodeData, _ct);
            await act.ShouldThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task StoreDeviceAuthorizationAsync_WhenDeviceCodeAlreadyExists_ExpectException()
    {
        var existingDeviceCode = $"device_{Guid.NewGuid()}";
        var deviceCodeData = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, $"sub_{Guid.NewGuid()}")]))
        };

        await using (var context = new PersistedGrantDbContext(_options))
        {
            context.DeviceFlowCodes.Add(new DeviceFlowCodes
            {
                DeviceCode = existingDeviceCode,
                UserCode = $"user_{Guid.NewGuid()}",
                ClientId = deviceCodeData.ClientId,
                SubjectId = deviceCodeData.Subject.FindFirst(JwtClaimTypes.Subject)!.Value,
                CreationTime = deviceCodeData.CreationTime,
                Expiration = deviceCodeData.CreationTime.AddSeconds(deviceCodeData.Lifetime),
                Data = _serializer.Serialize(deviceCodeData)
            });
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new PersistedGrantDbContext(_options))
        {
            var store = new DeviceFlowStore(context, new PersistentGrantSerializer(), new NullLogger<DeviceFlowStore>());

            // SQLite enforces unique constraints, so storing a duplicate device code throws.
            var act = () => store.StoreDeviceAuthorizationAsync(existingDeviceCode, $"user_{Guid.NewGuid()}", deviceCodeData, _ct);
            await act.ShouldThrowAsync<DbUpdateException>();
        }
    }
}
