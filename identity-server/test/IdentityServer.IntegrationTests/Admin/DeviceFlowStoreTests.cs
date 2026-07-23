// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class DeviceFlowStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IDeviceFlowStore BuildStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IDeviceFlowStore>();
    }

    [Fact]
    public async Task store_then_find_by_device_code_returns_data()
    {
        var store = BuildStore();

        var deviceCode = GenerateCode();
        var userCode = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);

        var result = await store.FindByDeviceCodeAsync(deviceCode, _ct);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe(data.ClientId);
        result.Lifetime.ShouldBe(data.Lifetime);
        result.IsOpenId.ShouldBe(data.IsOpenId);
        result.RequestedScopes.ShouldBe(data.RequestedScopes);
    }

    [Fact]
    public async Task store_then_find_by_user_code_returns_data()
    {
        var store = BuildStore();

        var deviceCode = GenerateCode();
        var userCode = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);

        var result = await store.FindByUserCodeAsync(userCode, _ct);

        result.ShouldNotBeNull();
        result.ClientId.ShouldBe(data.ClientId);
        result.Lifetime.ShouldBe(data.Lifetime);
    }

    [Fact]
    public async Task find_by_nonexistent_device_code_returns_null()
    {
        var store = BuildStore();

        var result = await store.FindByDeviceCodeAsync(GenerateCode(), _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task find_by_nonexistent_user_code_returns_null()
    {
        var store = BuildStore();

        var result = await store.FindByUserCodeAsync(GenerateCode(), _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task update_by_user_code_persists_changes()
    {
        var store = BuildStore();

        var deviceCode = GenerateCode();
        var userCode = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);

        // Simulate user authorization
        data.IsAuthorized = true;
        data.AuthorizedScopes = ["openid", "profile"];

        await store.UpdateByUserCodeAsync(userCode, data, _ct);

        var result = await store.FindByDeviceCodeAsync(deviceCode, _ct);

        result.ShouldNotBeNull();
        result.IsAuthorized.ShouldBeTrue();
        result.AuthorizedScopes.ShouldBe(["openid", "profile"]);
    }

    [Fact]
    public async Task update_nonexistent_user_code_throws()
    {
        var store = BuildStore();

        var data = CreateDeviceCode();

        await Should.ThrowAsync<InvalidOperationException>(
            () => store.UpdateByUserCodeAsync(GenerateCode(), data, _ct));
    }

    [Fact]
    public async Task remove_by_device_code_deletes_entry()
    {
        var store = BuildStore();

        var deviceCode = GenerateCode();
        var userCode = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);

        await store.RemoveByDeviceCodeAsync(deviceCode, _ct);

        var byDevice = await store.FindByDeviceCodeAsync(deviceCode, _ct);
        var byUser = await store.FindByUserCodeAsync(userCode, _ct);

        byDevice.ShouldBeNull();
        byUser.ShouldBeNull();
    }

    [Fact]
    public async Task remove_nonexistent_device_code_does_not_throw()
    {
        var store = BuildStore();

        // Should be idempotent — no exception
        await store.RemoveByDeviceCodeAsync(GenerateCode(), _ct);
    }

    [Fact]
    public async Task store_duplicate_device_code_throws()
    {
        var store = BuildStore();

        var deviceCode = GenerateCode();
        var userCode1 = GenerateCode();
        var userCode2 = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode, userCode1, data, _ct);

        await Should.ThrowAsync<InvalidOperationException>(
            () => store.StoreDeviceAuthorizationAsync(deviceCode, userCode2, data, _ct));
    }

    [Fact]
    public async Task store_duplicate_user_code_throws()
    {
        var store = BuildStore();

        var deviceCode1 = GenerateCode();
        var deviceCode2 = GenerateCode();
        var userCode = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode1, userCode, data, _ct);

        await Should.ThrowAsync<InvalidOperationException>(
            () => store.StoreDeviceAuthorizationAsync(deviceCode2, userCode, data, _ct));
    }

    [Fact]
    public async Task find_by_user_code_after_update_still_works()
    {
        var store = BuildStore();

        var deviceCode = GenerateCode();
        var userCode = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);

        data.IsAuthorized = true;
        await store.UpdateByUserCodeAsync(userCode, data, _ct);

        // Should still find by user code after update
        var result = await store.FindByUserCodeAsync(userCode, _ct);

        result.ShouldNotBeNull();
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task update_with_subject_round_trips_claims()
    {
        var store = BuildStore();

        var deviceCode = GenerateCode();
        var userCode = GenerateCode();
        var data = CreateDeviceCode();

        await store.StoreDeviceAuthorizationAsync(deviceCode, userCode, data, _ct);

        // Simulate user authorization with a ClaimsPrincipal
        data.IsAuthorized = true;
        data.Subject = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "alice"),
                new Claim("name", "Alice Smith"),
                new Claim("email", "alice@example.com")
            ],
            "pwd"));
        data.AuthorizedScopes = ["openid", "profile"];
        data.SessionId = "session_123";

        await store.UpdateByUserCodeAsync(userCode, data, _ct);

        var result = await store.FindByDeviceCodeAsync(deviceCode, _ct);

        result.ShouldNotBeNull();
        result.IsAuthorized.ShouldBeTrue();
        result.Subject.ShouldNotBeNull();
        result.Subject.FindFirst("sub")!.Value.ShouldBe("alice");
        result.Subject.FindFirst("name")!.Value.ShouldBe("Alice Smith");
        result.Subject.FindFirst("email")!.Value.ShouldBe("alice@example.com");
        result.AuthorizedScopes.ShouldBe(["openid", "profile"]);
        result.SessionId.ShouldBe("session_123");
    }

    private static string GenerateCode() => Guid.NewGuid().ToString("N");

    private static DeviceCode CreateDeviceCode() => new()
    {
        ClientId = "test_client",
        CreationTime = DateTime.UtcNow,
        Lifetime = 300,
        IsOpenId = true,
        RequestedScopes = ["openid", "api1"]
    };

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
