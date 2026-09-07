// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for IDeviceFlowStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class DeviceFlowStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance. Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<IDeviceFlowStore> CreateStore();

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task StoreDeviceAuthorizationAsync_WhenSuccessful_ExpectDeviceCodeAndUserCodeStored()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300
        };

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        }

        await using (var handle = CreateStore())
        {
            var foundByDeviceCode = await handle.Store.FindByDeviceCodeAsync(deviceCode, _ct);
            var foundByUserCode = await handle.Store.FindByUserCodeAsync(userCode, _ct);

            foundByDeviceCode.ShouldNotBeNull();
            foundByUserCode.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task StoreDeviceAuthorizationAsync_WhenSuccessful_ExpectDataStored()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = Guid.NewGuid().ToString(),
            CreationTime = DateTime.UtcNow,
            Lifetime = 300
        };

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        }

        await using (var handle = CreateStore())
        {
            var foundData = await handle.Store.FindByDeviceCodeAsync(deviceCode, _ct);

            foundData.ShouldNotBeNull();
            foundData.CreationTime.ShouldBeCloseTo(data.CreationTime, TimeSpan.FromSeconds(1));
            foundData.ClientId.ShouldBe(data.ClientId);
            foundData.Lifetime.ShouldBe(data.Lifetime);
        }
    }

    [Fact]
    public async Task FindByUserCodeAsync_WhenUserCodeExists_ExpectDataRetrievedCorrectly()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var expectedSubject = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, expectedSubject)]))
        };

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        }

        await using (var handle = CreateStore())
        {
            var code = await handle.Store.FindByUserCodeAsync(userCode, _ct);

            code.ShouldNotBeNull();
            code.ShouldSatisfyAllConditions(c =>
            {
                c.ClientId.ShouldBe(data.ClientId);
                c.RequestedScopes.ShouldBe(data.RequestedScopes);
                c.CreationTime.ShouldBeCloseTo(data.CreationTime, TimeSpan.FromSeconds(1));
                c.Lifetime.ShouldBe(data.Lifetime);
                c.IsOpenId.ShouldBe(data.IsOpenId);
            });

            code.Subject.ShouldNotBeNull();
            code.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task FindByUserCodeAsync_WhenUserCodeDoesNotExist_ExpectNull()
    {
        await using var handle = CreateStore();

        var code = await handle.Store.FindByUserCodeAsync(Guid.NewGuid().ToString(), _ct);

        code.ShouldBeNull();
    }

    [Fact]
    public async Task FindByDeviceCodeAsync_WhenDeviceCodeExists_ExpectDataRetrievedCorrectly()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var expectedSubject = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsOpenId = true,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, expectedSubject)]))
        };

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        }

        await using (var handle = CreateStore())
        {
            var code = await handle.Store.FindByDeviceCodeAsync(deviceCode, _ct);

            code.ShouldNotBeNull();
            code.ShouldSatisfyAllConditions(c =>
            {
                c.ClientId.ShouldBe(data.ClientId);
                c.CreationTime.ShouldBeCloseTo(data.CreationTime, TimeSpan.FromSeconds(1));
                c.Lifetime.ShouldBe(data.Lifetime);
                c.IsOpenId.ShouldBe(data.IsOpenId);
            });

            code.Subject.ShouldNotBeNull();
            code.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task FindByDeviceCodeAsync_WhenDeviceCodeDoesNotExist_ExpectNull()
    {
        await using var handle = CreateStore();

        var code = await handle.Store.FindByDeviceCodeAsync(Guid.NewGuid().ToString(), _ct);

        code.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateByUserCodeAsync_WhenDeviceCodeAuthorized_ExpectSubjectAndDataUpdated()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var expectedSubject = Guid.NewGuid().ToString();

        var unauthorizedDeviceCode = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsOpenId = true
        };

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreDeviceAuthorizationAsync(deviceCode, userCode, unauthorizedDeviceCode, _ct);
        }

        var authorizedDeviceCode = new DeviceCode
        {
            ClientId = unauthorizedDeviceCode.ClientId,
            RequestedScopes = unauthorizedDeviceCode.RequestedScopes,
            AuthorizedScopes = unauthorizedDeviceCode.RequestedScopes,
            Subject = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, expectedSubject)])),
            IsAuthorized = true,
            IsOpenId = true,
            CreationTime = unauthorizedDeviceCode.CreationTime,
            Lifetime = 300
        };

        await using (var handle = CreateStore())
        {
            await handle.Store.UpdateByUserCodeAsync(userCode, authorizedDeviceCode, _ct);
        }

        await using (var handle = CreateStore())
        {
            var code = await handle.Store.FindByDeviceCodeAsync(deviceCode, _ct);

            code.ShouldNotBeNull();
            code.ShouldSatisfyAllConditions(c =>
            {
                c.ClientId.ShouldBe(authorizedDeviceCode.ClientId);
                c.RequestedScopes.ShouldBe(authorizedDeviceCode.RequestedScopes);
                c.AuthorizedScopes.ShouldBe(authorizedDeviceCode.AuthorizedScopes);
                c.IsAuthorized.ShouldBe(true);
                c.IsOpenId.ShouldBe(true);
                c.CreationTime.ShouldBeCloseTo(authorizedDeviceCode.CreationTime, TimeSpan.FromSeconds(1));
                c.Lifetime.ShouldBe(authorizedDeviceCode.Lifetime);
            });

            code.Subject.ShouldNotBeNull();
            code.Subject.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject && x.Value == expectedSubject).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task RemoveByDeviceCodeAsync_WhenDeviceCodeExists_ExpectDeviceCodeDeleted()
    {
        var deviceCode = Guid.NewGuid().ToString();
        var userCode = Guid.NewGuid().ToString();
        var data = new DeviceCode
        {
            ClientId = "device_flow",
            RequestedScopes = new[] { "openid", "api1" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 300,
            IsOpenId = true
        };

        await using (var handle = CreateStore())
        {
            await handle.Store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);
        }

        await using (var handle = CreateStore())
        {
            await handle.Store.RemoveByDeviceCodeAsync(deviceCode, _ct);
        }

        await using (var handle = CreateStore())
        {
            var code = await handle.Store.FindByDeviceCodeAsync(deviceCode, _ct);
            code.ShouldBeNull();
        }
    }

    [Fact]
    public async Task RemoveByDeviceCodeAsync_WhenDeviceCodeDoesNotExists_ExpectSuccess()
    {
        await using var handle = CreateStore();

        await handle.Store.RemoveByDeviceCodeAsync(Guid.NewGuid().ToString(), _ct);
    }
}
