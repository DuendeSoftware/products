// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Stores;

public class InMemoryDeviceFlowStoreTests
{
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

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data);
        var foundData = await _store.FindByUserCodeAsync(userCode);

        foundData.ClientId.Should().Be(data.ClientId);
        foundData.CreationTime.Should().Be(data.CreationTime);
        foundData.Lifetime.Should().Be(data.Lifetime);
        foundData.IsAuthorized.Should().Be(data.IsAuthorized);
        foundData.IsOpenId.Should().Be(data.IsOpenId);
        foundData.Subject.Should().Be(data.Subject);
        foundData.RequestedScopes.Should().BeEquivalentTo(data.RequestedScopes);
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

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data);
        var foundData = await _store.FindByDeviceCodeAsync(deviceCode);

        foundData.ClientId.Should().Be(data.ClientId);
        foundData.CreationTime.Should().Be(data.CreationTime);
        foundData.Lifetime.Should().Be(data.Lifetime);
        foundData.IsAuthorized.Should().Be(data.IsAuthorized);
        foundData.IsOpenId.Should().Be(data.IsOpenId);
        foundData.Subject.Should().Be(data.Subject);
        foundData.RequestedScopes.Should().BeEquivalentTo(data.RequestedScopes);
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

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, initialData);

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

        await _store.UpdateByUserCodeAsync(userCode, updatedData);

        var foundData = await _store.FindByUserCodeAsync(userCode);

        foundData.ClientId.Should().Be(updatedData.ClientId);
        foundData.CreationTime.Should().Be(updatedData.CreationTime);
        foundData.Lifetime.Should().Be(updatedData.Lifetime);
        foundData.IsAuthorized.Should().Be(updatedData.IsAuthorized);
        foundData.IsOpenId.Should().Be(updatedData.IsOpenId);
        foundData.Subject.Should().BeEquivalentTo(updatedData.Subject);
        foundData.RequestedScopes.Should().BeEquivalentTo(updatedData.RequestedScopes);
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

        await _store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data);
        await _store.RemoveByDeviceCodeAsync(deviceCode);
        var foundData = await _store.FindByUserCodeAsync(userCode);

        foundData.Should().BeNull();
    }
}
