// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Stores;

public class InMemoryDeviceFlowStoreTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private InMemoryDeviceFlowStore _store = new InMemoryDeviceFlowStore();

    [Fact]
    public async Task StoreDeviceAuthorizationAsync_should_persist_data_by_user_code()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsAuthorized = false,
            IsOpenId = true,
            Subject = null,
            RequestedScopes = new[] { "scope1", "scope2" }
        };

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        var foundData = await _store.FindByUserCodeAsync(userCode, _ct);

        foundData.ClientId.ShouldBe(data.ClientId);
        foundData.CreationTime.ShouldBe(data.CreationTime);
        foundData.Lifetime.ShouldBe(data.Lifetime);
        foundData.IsAuthorized.ShouldBe(data.IsAuthorized);
        foundData.IsOpenId.ShouldBe(data.IsOpenId);
        foundData.Subject.ShouldBe(data.Subject);
        foundData.RequestedScopes.ShouldBe(data.RequestedScopes);
    }

    [Fact]
    public async Task StoreDeviceAuthorizationAsync_should_persist_data_by_device_code()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsAuthorized = false,
            IsOpenId = true,
            Subject = null,
            RequestedScopes = new[] { "scope1", "scope2" }
        };

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        var foundData = await _store.FindByDeviceCodeAsync(deviceCode, _ct);

        foundData.ClientId.ShouldBe(data.ClientId);
        foundData.CreationTime.ShouldBe(data.CreationTime);
        foundData.Lifetime.ShouldBe(data.Lifetime);
        foundData.IsAuthorized.ShouldBe(data.IsAuthorized);
        foundData.IsOpenId.ShouldBe(data.IsOpenId);
        foundData.Subject.ShouldBe(data.Subject);
        foundData.RequestedScopes.ShouldBe(data.RequestedScopes);
    }

    [Fact]
    public async Task UpdateByUserCodeAsync_should_update_data()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var initialData = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsAuthorized = false,
            IsOpenId = true,
            Subject = null,
            RequestedScopes = new[] { "scope1", "scope2" }
        };

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, initialData, _ct);

        var updatedData = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = initialData.CreationTime.AddHours(2),
            Lifetime = initialData.Lifetime + 600,
            IsAuthorized = !initialData.IsAuthorized,
            IsOpenId = !initialData.IsOpenId,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim("sub", "123") })),
            RequestedScopes = new[] { "api1", "api2" }
        };

        await _store.UpdateByUserCodeAsync(userCode, updatedData, _ct);

        var foundData = await _store.FindByUserCodeAsync(userCode, _ct);

        foundData.ClientId.ShouldBe(updatedData.ClientId);
        foundData.CreationTime.ShouldBe(updatedData.CreationTime);
        foundData.Lifetime.ShouldBe(updatedData.Lifetime);
        foundData.IsAuthorized.ShouldBe(updatedData.IsAuthorized);
        foundData.IsOpenId.ShouldBe(updatedData.IsOpenId);
        foundData.Subject.ShouldBe(updatedData.Subject);
        foundData.RequestedScopes.ShouldBe(updatedData.RequestedScopes);
    }

    [Fact]
    public async Task RemoveByDeviceCodeAsync_should_update_data()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsAuthorized = false,
            IsOpenId = true,
            Subject = null,
            RequestedScopes = new[] { "scope1", "scope2" }
        };

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        await _store.RemoveByDeviceCodeAsync(deviceCode, _ct);
        var foundData = await _store.FindByUserCodeAsync(userCode, _ct);

        foundData.ShouldBeNull();
    }
}
