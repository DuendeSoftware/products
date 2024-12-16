// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using FluentAssertions;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using UnitTests.Common;
using UnitTests.Validation.Setup;
using Xunit;

namespace UnitTests.Services.Default;

public class DefaultRefreshTokenServiceTests
{
    private DefaultRefreshTokenService _subject;
    private DefaultRefreshTokenStore _store;
    private PersistentGrantOptions _options;

    private ClaimsPrincipal _user = new IdentityServerUser("123").CreatePrincipal();
    private StubClock _clock = new StubClock();

    public DefaultRefreshTokenServiceTests()
    {
        _options = new PersistentGrantOptions();

        _store = new DefaultRefreshTokenStore(
            new InMemoryPersistedGrantStore(),
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultRefreshTokenStore>());

        _subject = new DefaultRefreshTokenService(
            _store,
            new TestProfileService(),
            _clock,
            _options,
            TestLogger.Create<DefaultRefreshTokenService>());
    }

    [Fact]
    public async Task CreateRefreshToken_token_exists_in_store()
    {
        var client = new Client();
        var accessToken = new Token();

        var handle = await _subject.CreateRefreshTokenAsync(new RefreshTokenCreationRequest { Subject = _user, AccessToken = accessToken, Client = client });

        (await _store.GetRefreshTokenAsync(handle)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateRefreshToken_should_match_absolute_lifetime()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Absolute,
            AbsoluteRefreshTokenLifetime = 10
        };

        var handle = await _subject.CreateRefreshTokenAsync(new RefreshTokenCreationRequest { Subject = _user, AccessToken = new Token(), Client = client });

        var refreshToken = (await _store.GetRefreshTokenAsync(handle));

        refreshToken.Should().NotBeNull();
        refreshToken.Lifetime.Should().Be(client.AbsoluteRefreshTokenLifetime);
    }

    [Fact]
    public async Task CreateRefreshToken_should_cap_sliding_lifetime_that_exceeds_absolute_lifetime()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 100,
            AbsoluteRefreshTokenLifetime = 10
        };

        var handle = await _subject.CreateRefreshTokenAsync(new RefreshTokenCreationRequest { Subject = _user, AccessToken = new Token(), Client = client });

        var refreshToken = (await _store.GetRefreshTokenAsync(handle));

        refreshToken.Should().NotBeNull();
        refreshToken.Lifetime.Should().Be(client.AbsoluteRefreshTokenLifetime);
    }

    [Fact]
    public async Task CreateRefreshToken_should_match_sliding_lifetime()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 10
        };

        var handle = await _subject.CreateRefreshTokenAsync(new RefreshTokenCreationRequest { Subject = _user, AccessToken = new Token(), Client = client });

        var refreshToken = (await _store.GetRefreshTokenAsync(handle));

        refreshToken.Should().NotBeNull();
        refreshToken.Lifetime.Should().Be(client.SlidingRefreshTokenLifetime);
    }


    [Fact]
    public async Task UpdateRefreshToken_one_time_use_should_create_new_token()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        (await _subject.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest { Handle = handle, RefreshToken = refreshToken, Client = client }))
            .Should().NotBeNull()
            .And
            .NotBe(handle);
    }

    [Fact]
    public async Task UpdateRefreshToken_sliding_with_non_zero_absolute_should_update_lifetime()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 10,
            AbsoluteRefreshTokenLifetime = 100
        };

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var handle = await _store.StoreRefreshTokenAsync(new RefreshToken
        {
            CreationTime = now.AddSeconds(-10),
        });

        var refreshToken = await _store.GetRefreshTokenAsync(handle);
        var newHandle = await _subject.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest { Handle = handle, RefreshToken = refreshToken, Client = client });

        newHandle.Should().NotBeNull().And.Be(handle);

        var newRefreshToken = await _store.GetRefreshTokenAsync(newHandle);

        newRefreshToken.Should().NotBeNull();
        newRefreshToken.Lifetime.Should().Be((int)(now - newRefreshToken.CreationTime).TotalSeconds + client.SlidingRefreshTokenLifetime);
    }

    [Fact]
    public async Task UpdateRefreshToken_lifetime_exceeds_absolute_should_be_absolute_lifetime()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 10,
            AbsoluteRefreshTokenLifetime = 1000
        };

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var handle = await _store.StoreRefreshTokenAsync(new RefreshToken
        {
            CreationTime = now.AddSeconds(-1000),
        });

        var refreshToken = await _store.GetRefreshTokenAsync(handle);
        var newHandle = await _subject.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest { Handle = handle, RefreshToken = refreshToken, Client = client });

        newHandle.Should().NotBeNull().And.Be(handle);

        var newRefreshToken = await _store.GetRefreshTokenAsync(newHandle);

        newRefreshToken.Should().NotBeNull();
        newRefreshToken.Lifetime.Should().Be(client.AbsoluteRefreshTokenLifetime);
    }

    [Fact]
    public async Task UpdateRefreshToken_sliding_with_zero_absolute_should_update_lifetime()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 10,
            AbsoluteRefreshTokenLifetime = 0
        };

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var handle = await _store.StoreRefreshTokenAsync(new RefreshToken
        {
            CreationTime = now.AddSeconds(-1000),
        });

        var refreshToken = await _store.GetRefreshTokenAsync(handle);
        var newHandle = await _subject.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest { Handle = handle, RefreshToken = refreshToken, Client = client });

        newHandle.Should().NotBeNull().And.Be(handle);

        var newRefreshToken = await _store.GetRefreshTokenAsync(newHandle);

        newRefreshToken.Should().NotBeNull();
        newRefreshToken.Lifetime.Should().Be((int)(now - newRefreshToken.CreationTime).TotalSeconds + client.SlidingRefreshTokenLifetime);
    }

    [Fact]
    public async Task UpdateRefreshToken_for_onetime_and_sliding_with_zero_absolute_should_update_lifetime()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 10,
            AbsoluteRefreshTokenLifetime = 0
        };

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var handle = await _store.StoreRefreshTokenAsync(new RefreshToken
        {
            ClientId = client.ClientId,
            Subject = _user,
            CreationTime = now.AddSeconds(-1000),
        });

        var refreshToken = await _store.GetRefreshTokenAsync(handle);
        var newHandle = await _subject.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest { Handle = handle, RefreshToken = refreshToken, Client = client });

        newHandle.Should().NotBeNull().And.NotBe(handle);

        var newRefreshToken = await _store.GetRefreshTokenAsync(newHandle);

        newRefreshToken.Should().NotBeNull();
        newRefreshToken.Lifetime.Should().Be((int)(now - newRefreshToken.CreationTime).TotalSeconds + client.SlidingRefreshTokenLifetime);
    }

    [Fact]
    public async Task UpdateRefreshToken_one_time_use_with_consume_on_use_should_consume_token_and_create_new_one_with_correct_dates()
    {
        _options.DeleteOneTimeOnlyRefreshTokensOnUse = false;
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            ClientId = client.ClientId,
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var newHandle = await _subject.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest { Handle = handle, RefreshToken = refreshToken, Client = client });

        var oldToken = await _store.GetRefreshTokenAsync(handle);
        var newToken = await _store.GetRefreshTokenAsync(newHandle);

        oldToken.ConsumedTime.Should().Be(now);
        newToken.ConsumedTime.Should().BeNull();

        newToken.CreationTime.Should().Be(oldToken.CreationTime);
        newToken.Lifetime.Should().Be(oldToken.Lifetime);
    }

    [Fact]
    public async Task UpdateRefreshToken_one_time_use_with_delete_should_delete_on_use_token_and_create_new_one_with_correct_dates()
    {
        _options.DeleteOneTimeOnlyRefreshTokensOnUse = true;
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            ClientId = client.ClientId,
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var newHandle = await _subject.UpdateRefreshTokenAsync(new RefreshTokenUpdateRequest { Handle = handle, RefreshToken = refreshToken, Client = client });

        var oldToken = await _store.GetRefreshTokenAsync(handle);
        var newToken = await _store.GetRefreshTokenAsync(newHandle);

        oldToken.Should().BeNull();
        newToken.ConsumedTime.Should().BeNull();

        newToken.CreationTime.Should().Be(refreshToken.CreationTime);
        newToken.Lifetime.Should().Be(refreshToken.Lifetime);
    }

    [Fact]
    public async Task ValidateRefreshToken_invalid_token_should_fail()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var result = await _subject.ValidateRefreshTokenAsync("invalid", client);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshToken_client_without_allow_offline_access_should_fail()
    {
        var client = new Client
        {
            ClientId = "client1",
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            ClientId = client.ClientId,
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var result = await _subject.ValidateRefreshTokenAsync(handle, client);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshToken_invalid_client_binding_should_fail()
    {
        var client = new Client
        {
            ClientId = "client1",
            AllowOfflineAccess = true,
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            ClientId = "client2",
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var result = await _subject.ValidateRefreshTokenAsync(handle, client);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshToken_expired_token_should_fail()
    {
        var client = new Client
        {
            ClientId = "client1",
            AllowOfflineAccess = true,
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            ClientId = client.ClientId,
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        var now = DateTime.UtcNow.AddSeconds(20);
        _clock.UtcNowFunc = () => now;

        var result = await _subject.ValidateRefreshTokenAsync(handle, client);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshToken_consumed_token_should_fail()
    {
        var client = new Client
        {
            ClientId = "client1",
            AllowOfflineAccess = true,
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            ConsumedTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Subject = _user,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var result = await _subject.ValidateRefreshTokenAsync(handle, client);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshToken_valid_token_should_succeed()
    {
        var client = new Client
        {
            ClientId = "client1",
            AllowOfflineAccess = true,
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            ClientId = client.ClientId,
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle = await _store.StoreRefreshTokenAsync(refreshToken);

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var result = await _subject.ValidateRefreshTokenAsync(handle, client);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshToken_valid_token_should_accept_v5_token()
    {
        var client = new Client
        {
            ClientId = "client1",
            AllowOfflineAccess = true,
            RefreshTokenUsage = TokenUsage.OneTimeOnly
        };

        var refreshToken = new RefreshToken
        {
            ClientId = client.ClientId,
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        // force create in DB with this key value (pre-v6 format)
        await _store.UpdateRefreshTokenAsync("key", refreshToken);

        var now = DateTime.UtcNow;
        _clock.UtcNowFunc = () => now;

        var result = await _subject.ValidateRefreshTokenAsync("key", client);

        result.IsError.Should().BeFalse();
    }
}
