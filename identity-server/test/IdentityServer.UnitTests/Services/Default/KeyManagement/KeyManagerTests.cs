// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.KeyManagement;
using Microsoft.Extensions.Logging;
using UnitTests.Validation.Setup;

namespace UnitTests.Services.Default.KeyManagement;

public class KeyManagerTests
{
    private KeyManager _subject;

    private SigningAlgorithmOptions _rsaOptions = new SigningAlgorithmOptions("RS256");

    private IdentityServerOptions _options = new IdentityServerOptions();

    private MockSigningKeyStore _mockKeyStore = new MockSigningKeyStore();
    private MockSigningKeyStoreCache _mockKeyStoreCache = new MockSigningKeyStoreCache();
    private MockSigningKeyProtector _mockKeyProtector = new MockSigningKeyProtector();
    private MockClock _mockClock = new MockClock(new DateTime(2018, 3, 10, 9, 0, 0));

    public KeyManagerTests()
    {
        // just to speed up the tests
        _options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.FromMilliseconds(1);

        _options.KeyManagement.SigningAlgorithms = new[] { _rsaOptions };

        _subject = new KeyManager(
            _options,
            _mockKeyStore,
            _mockKeyStoreCache,
            _mockKeyProtector,
            _mockClock,
            new NopConcurrencyLock<KeyManager>(),
            new LoggerFactory().CreateLogger<KeyManager>(),
            new TestIssuerNameService());
    }

    private SerializedKey CreateSerializedKey(TimeSpan? age = null, string alg = "RS256", bool x509 = false)
    {
        var container = CreateKey(age, alg, x509);
        return _mockKeyProtector.Protect(container);
    }

    private KeyContainer CreateKey(TimeSpan? age = null, string alg = "RS256", bool x509 = false)
    {
        var key = _options.KeyManagement.CreateRsaSecurityKey();

        var date = _mockClock.UtcNow.UtcDateTime;
        if (age.HasValue) date = date.Subtract(age.Value);

        var container = x509 ?
            new X509KeyContainer(key, alg, date, _options.KeyManagement.KeyRetirementAge) :
            (KeyContainer)new RsaKeyContainer(key, alg, date);

        return container;
    }

    private string CreateAndStoreKey(TimeSpan? age = null)
    {
        var container = CreateKey(age);
        _mockKeyStore.Keys.Add(_mockKeyProtector.Protect(container));
        return container.Id;
    }

    private string CreateAndStoreKeyThatCannotBeUnprotected(TimeSpan? age = null)
    {
        var container = CreateKey(age);
        _mockKeyStore.Keys.Add(_mockKeyProtector.ProtectAndLoseDataProtectionKey(container));
        return container.Id;
    }

    private string CreateCacheAndStoreKey(TimeSpan? age = null)
    {
        var container = CreateKey(age);
        _mockKeyStore.Keys.Add(_mockKeyProtector.Protect(container));
        _mockKeyStoreCache.Cache.Add(container);
        return container.Id;
    }

    // ctor

    [Fact]
    public void ctor_should_validate_options()
    {
        _options.KeyManagement.PropagationTime = TimeSpan.Zero;

        var a = () =>
        {
            _subject = new KeyManager(
                _options,
                _mockKeyStore,
                _mockKeyStoreCache,
                _mockKeyProtector,
                _mockClock,
                new NopConcurrencyLock<KeyManager>(),
                new LoggerFactory().CreateLogger<KeyManager>(),
                new TestIssuerNameService());
        };
        a.ShouldThrow<Exception>();
    }

    // GetCurrentKeysAsync

    [Fact]
    public async Task GetCurrentKeysAsync_should_return_key()
    {
        var id = CreateAndStoreKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromHours(1)));

        var keys = await _subject.GetCurrentKeysAsync();
        var key = keys.Single();
        key.Id.ShouldBe(id);
    }

    // GetAllKeysInternalAsync

    [Fact]
    public async Task GetAllKeysInternalAsync_when_valid_key_exists_should_use_key()
    {
        var id = CreateAndStoreKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromHours(1)));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();
        key.Id.ShouldBe(id);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_recently_created_key_exists_should_use_key()
    {
        var id = CreateAndStoreKey(TimeSpan.FromSeconds(5));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        _mockKeyStore.Keys.Count.ShouldBe(1);
        _mockKeyStore.Keys.Single().Id.ShouldBe(key.Id);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_only_one_key_created_in_future_should_use_key()
    {
        var id = CreateAndStoreKey(-TimeSpan.FromSeconds(5));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        _mockKeyStore.Keys.Count.ShouldBe(1);
        _mockKeyStore.Keys.Single().Id.ShouldBe(key.Id);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_no_keys_should_create_key()
    {
        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        _mockKeyStore.Keys.Count.ShouldBe(1);
        _mockKeyStore.Keys.Single().Id.ShouldBe(key.Id);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_null_keys_should_create_key()
    {
        _mockKeyStore.Keys = null;

        var (keys, key) = await _subject.GetAllKeysInternalAsync();

        keys.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_all_keys_are_expired_should_create_key()
    {
        var id = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(5)));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        _mockKeyStore.Keys.Count.ShouldBe(2);
        key.Id.ShouldNotBe(id);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_all_keys_are_expired_should_requery_database_and_use_valid_db_key()
    {
        var id1 = CreateCacheAndStoreKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(5)));
        var id2 = CreateAndStoreKey();

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        key.Id.ShouldBe(id2);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_should_use_oldest_active_key()
    {
        var key1 = CreateAndStoreKey(TimeSpan.FromSeconds(10));
        var key2 = CreateAndStoreKey(TimeSpan.FromSeconds(5));
        var key3 = CreateAndStoreKey(-TimeSpan.FromSeconds(5));
        var key4 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(5)));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        _mockKeyStore.Keys.Count.ShouldBe(4);
        key.Id.ShouldBe(key1);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_should_ignore_keys_not_yet_activated()
    {
        var key1 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(10)));
        var key2 = CreateAndStoreKey(-TimeSpan.FromSeconds(5));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        _mockKeyStore.Keys.Count.ShouldBe(2);
        key.Id.ShouldBe(key1);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_cache_empty_should_return_non_retired_keys_from_store()
    {
        var key1 = CreateAndStoreKey(TimeSpan.FromSeconds(10));
        var key2 = CreateAndStoreKey(TimeSpan.FromSeconds(5));
        var key3 = CreateAndStoreKey(-TimeSpan.FromSeconds(5));
        var key4 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(5)));
        var key5 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(5)));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        allKeys.Select(x => x.Id).ShouldBe([key1, key2, key3, key4]);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_cache_null_should_return_non_retired_keys_from_store()
    {
        _mockKeyStoreCache.Cache = null;

        var key1 = CreateAndStoreKey(TimeSpan.FromSeconds(10));
        var key2 = CreateAndStoreKey(TimeSpan.FromSeconds(5));
        var key3 = CreateAndStoreKey(-TimeSpan.FromSeconds(5));
        var key4 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(5)));
        var key5 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(5)));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        allKeys.Select(x => x.Id).ShouldBe([key1, key2, key3, key4]);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_cache_empty_should_update_the_cache()
    {
        var key = CreateAndStoreKey();

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        allKeys.Count().ShouldBe(1);
        allKeys.Single().Id.ShouldBe(key);
        _mockKeyStoreCache.StoreKeysAsyncWasCalled.ShouldBeTrue();
        _mockKeyStoreCache.Cache.Count.ShouldBe(1);
        _mockKeyStoreCache.Cache.Single().Id.ShouldBe(key);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_should_use_the_cache()
    {
        var key = CreateKey();
        _mockKeyStoreCache.Cache = new List<KeyContainer>()
        {
            key
        };

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        allKeys.Count().ShouldBe(1);
        allKeys.Single().Id.ShouldBe(key.Id);
        _mockKeyStore.LoadKeysAsyncWasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_key_rotation_is_needed_should_create_new_key()
    {
        var key1 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1)));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.ShouldNotBeNull();
        key.Id.ShouldBe(key1);
        _mockKeyStore.Keys.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_key_rotation_is_needed_for_cached_keys_should_requery_database_to_determine_if_rotation_still_needed()
    {
        var key1 = CreateCacheAndStoreKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1)));
        var key2 = CreateAndStoreKey();

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        _mockKeyStore.Keys.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllKeysInternalAsync_when_key_rotation_is_not_needed_should_not_create_new_key()
    {
        var key1 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Subtract(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1))));

        var (allKeys, signgingKeys) = await _subject.GetAllKeysInternalAsync();

        var key = signgingKeys.Single();

        key.Id.ShouldBe(key1);
        _mockKeyStore.Keys.Count.ShouldBe(1);
    }

    // GetKeysFromCacheAsync

    [Fact]
    public async Task GetKeysFromCacheAsync_should_use_cache()
    {
        var id = CreateCacheAndStoreKey();

        var keys = await _subject.GetAllKeysFromCacheAsync();

        keys.Count().ShouldBe(1);
        keys.Single().Id.ShouldBe(id);
        _mockKeyStore.LoadKeysAsyncWasCalled.ShouldBeFalse();
    }

    // AreAllKeysWithinInitializationDuration

    [Fact]
    public void AreAllKeysWithinInitializationDuration_should_ignore_retired_and_expired_keys()
    {
        {
            var key1 = CreateKey(_options.KeyManagement.KeyRetirementAge);
            var key2 = CreateKey(_options.KeyManagement.RotationInterval);
            var key3 = CreateKey(_options.KeyManagement.InitializationDuration.Add(-TimeSpan.FromSeconds(1)));

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key1, key2, key3 });

            result.ShouldBeTrue();
        }
        {
            var key1 = CreateKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(1)));
            var key2 = CreateKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(1)));
            var key3 = CreateKey(_options.KeyManagement.InitializationDuration.Add(-TimeSpan.FromSeconds(1)));

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key1, key2, key3 });

            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void AreAllKeysWithinInitializationDuration_for_new_keys_should_return_true()
    {
        {
            var key1 = CreateKey(_options.KeyManagement.InitializationDuration.Add(-TimeSpan.FromSeconds(1)));

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key1 });

            result.ShouldBeTrue();
        }
        {
            var key1 = CreateKey(_options.KeyManagement.InitializationDuration);

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key1 });

            result.ShouldBeTrue();
        }
        {
            var key1 = CreateKey();

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key1 });

            result.ShouldBeTrue();
        }
        {
            var key1 = CreateKey(_options.KeyManagement.InitializationDuration);
            var key2 = CreateKey(_options.KeyManagement.InitializationDuration.Add(-TimeSpan.FromSeconds(1)));
            var key3 = CreateKey();

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key1, key2, key3 });

            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void AreAllKeysWithinInitializationDuration_for_older_keys_should_return_false()
    {
        {
            var key0 = CreateKey(_options.KeyManagement.InitializationDuration.Add(TimeSpan.FromSeconds(1)));

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key0 });

            result.ShouldBeFalse();
        }
        {
            var key0 = CreateKey(_options.KeyManagement.InitializationDuration.Add(TimeSpan.FromSeconds(1)));
            var key1 = CreateKey(_options.KeyManagement.InitializationDuration.Add(-TimeSpan.FromSeconds(1)));

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key0, key1 });

            result.ShouldBeFalse();
        }
        {
            var key0 = CreateKey(_options.KeyManagement.InitializationDuration.Add(TimeSpan.FromSeconds(1)));
            var key1 = CreateKey(_options.KeyManagement.InitializationDuration);

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key0, key1 });

            result.ShouldBeFalse();
        }
        {
            var key0 = CreateKey(_options.KeyManagement.InitializationDuration.Add(TimeSpan.FromSeconds(1)));
            var key1 = CreateKey();

            var result = _subject.AreAllKeysWithinInitializationDuration(new[] { key0, key1 });

            result.ShouldBeFalse();
        }
        {
            var key0 = CreateKey(_options.KeyManagement.InitializationDuration.Add(TimeSpan.FromSeconds(1)));
            var key1 = CreateKey(_options.KeyManagement.InitializationDuration);
            var key2 = CreateKey(_options.KeyManagement.InitializationDuration.Add(-TimeSpan.FromSeconds(1)));
            var key3 = CreateKey();

            var result = _subject.AreAllKeysWithinInitializationDuration([key0, key1, key2, key3]);

            result.ShouldBeFalse();
        }
    }

    // FilterAndDeleteRetiredKeysAsync

    [Fact]
    public async Task FilterRetiredKeys_should_filter_retired_keys()
    {
        var key1 = CreateSerializedKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(1)));
        var key2 = CreateSerializedKey(_options.KeyManagement.KeyRetirementAge);
        var key3 = CreateSerializedKey(_options.KeyManagement.KeyRetirementAge.Subtract(TimeSpan.FromSeconds(1)));
        var key4 = CreateSerializedKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)));
        var key5 = CreateSerializedKey(_options.KeyManagement.PropagationTime);
        var key6 = CreateSerializedKey(_options.KeyManagement.PropagationTime.Subtract(TimeSpan.FromSeconds(1)));

        var result = await _subject.FilterAndDeleteRetiredKeysAsync([key1, key2, key3, key4, key5, key6]);

        result.Select(x => x.Id).ShouldBe([key3.Id, key4.Id, key5.Id, key6.Id]);
    }

    [Fact]
    public async Task FilterRetiredKeys_should_delete_from_database()
    {
        _options.KeyManagement.DeleteRetiredKeys = true;

        var key1 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(1)));
        var key2 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge);
        var key3 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge.Subtract(TimeSpan.FromSeconds(1)));
        var key4 = CreateAndStoreKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)));
        var key5 = CreateAndStoreKey(_options.KeyManagement.PropagationTime);
        var key6 = CreateAndStoreKey(_options.KeyManagement.PropagationTime.Subtract(TimeSpan.FromSeconds(1)));

        var keys = await _subject.GetAllKeysAsync();

        _mockKeyStore.DeleteWasCalled.ShouldBeTrue();
        _mockKeyStore.Keys.Select(x => x.Id).ShouldBe([key3, key4, key5, key6]);
    }

    [Fact]
    public async Task FilterRetiredKeys_when_delete_disabled_should_not_delete_from_database()
    {
        _options.KeyManagement.DeleteRetiredKeys = false;

        var key1 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(1)));
        var key2 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge);
        var key3 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge.Subtract(TimeSpan.FromSeconds(1)));
        var key4 = CreateAndStoreKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)));
        var key5 = CreateAndStoreKey(_options.KeyManagement.PropagationTime);
        var key6 = CreateAndStoreKey(_options.KeyManagement.PropagationTime.Subtract(TimeSpan.FromSeconds(1)));

        var keys = await _subject.GetAllKeysAsync();

        _mockKeyStore.DeleteWasCalled.ShouldBeFalse();
        _mockKeyStore.Keys.Select(x => x.Id).ShouldBe([key1, key2, key3, key4, key5, key6]);
    }

    // FilterExpiredKeys

    [Fact]
    public void FilterExpiredKeys_should_filter_expired_keys()
    {
        var key1 = CreateKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(1)));
        var key2 = CreateKey(_options.KeyManagement.RotationInterval);
        var key3 = CreateKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1)));
        var key4 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)));
        var key5 = CreateKey(_options.KeyManagement.PropagationTime);
        var key6 = CreateKey(_options.KeyManagement.PropagationTime.Subtract(TimeSpan.FromSeconds(1)));

        var result = _subject.FilterExpiredKeys(new[] { key1, key2, key3, key4, key5, key6 });

        result.Select(x => x.Id).ShouldBe([key3.Id, key4.Id, key5.Id, key6.Id]);
    }

    // CacheKeysAsync

    [Fact]
    public async Task CacheKeysAsync_should_not_store_empty_keys()
    {
        {
            await _subject.CacheKeysAsync(null);

            _mockKeyStoreCache.StoreKeysAsyncWasCalled.ShouldBeFalse();
        }

        {
            await _subject.CacheKeysAsync(new RsaKeyContainer[0]);

            _mockKeyStoreCache.StoreKeysAsyncWasCalled.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task CacheKeysAsync_should_store_keys_in_cache_with_normal_cache_duration()
    {
        var key1 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromMinutes(5)));
        var key2 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromMinutes(10)));

        await _subject.CacheKeysAsync(new[] { key1, key2 });

        _mockKeyStoreCache.StoreKeysAsyncWasCalled.ShouldBeTrue();
        _mockKeyStoreCache.StoreKeysAsyncDuration.ShouldBe(_options.KeyManagement.KeyCacheDuration);

        _mockKeyStoreCache.Cache.Select(x => x.Id).ShouldBe(new[] { key1.Id, key2.Id });
    }

    [Fact]
    public async Task CacheKeysAsync_when_keys_are_new_should_use_initialization_duration()
    {
        var key1 = CreateKey();

        await _subject.CacheKeysAsync(new[] { key1 });

        _mockKeyStoreCache.StoreKeysAsyncWasCalled.ShouldBeTrue();
        _mockKeyStoreCache.StoreKeysAsyncDuration.ShouldBe(_options.KeyManagement.InitializationKeyCacheDuration);
    }

    // GetKeysFromStoreAsync

    [Fact]
    public async Task GetKeysFromStoreAsync_should_use_store_and_cache_keys()
    {
        var key = CreateAndStoreKey();

        var keys = await _subject.GetAllKeysFromStoreAsync();

        keys.ShouldNotBeNull();
        keys.Single().Id.ShouldBe(key);
        _mockKeyStoreCache.GetKeysAsyncWasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task GetKeysFromStoreAsync_should_filter_retired_keys()
    {
        var key1 = CreateAndStoreKey(TimeSpan.FromSeconds(10));
        var key2 = CreateAndStoreKey(TimeSpan.FromSeconds(5));
        var key3 = CreateAndStoreKey(-TimeSpan.FromSeconds(5));
        var key4 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(1)));
        var key5 = CreateAndStoreKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(5)));

        var keys = await _subject.GetAllKeysFromStoreAsync();

        keys.Select(x => x.Id).ShouldBe([key1, key2, key3, key4]);
    }

    [Fact]
    public async Task GetKeysFromStoreAsync_should_filter_retired_keys_that_cannot_be_unprotected()
    {
        var key1 = CreateAndStoreKey(TimeSpan.FromSeconds(10));
        var key2 = CreateAndStoreKey(TimeSpan.FromSeconds(5));
        var key3 = CreateAndStoreKey(-TimeSpan.FromSeconds(5));
        var key4 = CreateAndStoreKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(1)));
        var key5 = CreateAndStoreKeyThatCannotBeUnprotected(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromSeconds(5)));

        var keys = await _subject.GetAllKeysFromStoreAsync();

        keys.Select(x => x.Id).ShouldBe([key1, key2, key3, key4]);

        _mockKeyStore.DeleteWasCalled.ShouldBeTrue();
        var keysInStore = await _mockKeyStore.LoadKeysAsync();
        keysInStore.Select(x => x.Id).ShouldBe([key1, key2, key3, key4]);
    }

    [Fact]
    public async Task GetKeysFromStoreAsync_should_filter_null_keys()
    {
        var key1 = CreateAndStoreKey(TimeSpan.FromSeconds(10));
        _mockKeyStore.Keys.Add(null);

        var keys = await _subject.GetAllKeysFromStoreAsync();

        keys.Select(x => x.Id).ShouldBe([key1]);
    }

    // CreateNewKeysAndAddToCacheAsync

    [Fact]
    public async Task CreateNewKeyAndAddToCacheAsync_when_no_keys_should_store_and_return_new_key()
    {
        var (allKeys, signingKeys) = await _subject.CreateNewKeysAndAddToCacheAsync();
        var key = signingKeys.Single();
        _mockKeyStore.Keys.Single().Id.ShouldBe(key.Id);
    }

    [Fact]
    public async Task CreateNewKeyAndAddToCacheAsync_when_existing_keys_should_store_and_return_active_key()
    {
        var key1 = CreateCacheAndStoreKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)));

        var (allKeys, signingKeys) = await _subject.CreateNewKeysAndAddToCacheAsync();
        var key = signingKeys.Single();

        allKeys.Count().ShouldBe(2);
        _mockKeyStore.Keys.Count.ShouldBe(2);

        key.Id.ShouldBe(key1);
    }

    [Fact]
    public async Task CreateNewKeyAndAddToCacheAsync_should_return_all_keys()
    {
        var key1 = CreateCacheAndStoreKey();

        var (allKeys, signingKeys) = await _subject.CreateNewKeysAndAddToCacheAsync();

        allKeys.Select(x => x.Id).ShouldBe(_mockKeyStore.Keys.Select(x => x.Id));
    }

    [Fact]
    public async Task CreateNewKeyAndAddToCacheAsync_when_keys_are_new_should_delay_for_initialization_and_synchronization_delay()
    {
        _options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.FromSeconds(5);

        var key1 = CreateCacheAndStoreKey();

        var sw = new Stopwatch();
        sw.Start();
        var (allKeys, signingKeys) = await _subject.CreateNewKeysAndAddToCacheAsync();
        sw.Stop();

        sw.Elapsed.ShouldBeGreaterThanOrEqualTo(_options.KeyManagement.InitializationSynchronizationDelay);

        allKeys.Select(x => x.Id).ShouldBe(_mockKeyStore.Keys.Select(x => x.Id));
    }

    [Fact]
    public async Task CreateNewKeyAndAddToCacheAsync_when_keys_are_old_should_not_delay_for_initialization_and_synchronization_delay()
    {
        _options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.FromMinutes(1);

        var key1 = CreateCacheAndStoreKey(_options.KeyManagement.InitializationDuration.Add(TimeSpan.FromSeconds(1)));

        var sw = new Stopwatch();
        sw.Start();
        var (allKeys, signingKeys) = await _subject.CreateNewKeysAndAddToCacheAsync();
        sw.Stop();

        sw.Elapsed.ShouldBeLessThan(_options.KeyManagement.InitializationSynchronizationDelay);

        allKeys.Select(x => x.Id).ShouldBe(_mockKeyStore.Keys.Select(x => x.Id));
    }

    // GetCurrentSigningKey

    [Fact]
    public void GetActiveSigningKey_for_no_keys_should_return_null()
    {
        {
            var key = _subject.GetAllCurrentSigningKeys(null);
            key.ShouldBeEmpty();
        }
        {
            var key = _subject.GetAllCurrentSigningKeys(new RsaKeyContainer[0]);
            key.ShouldBeEmpty();
        }
    }

    // GetCurrentSigningKeyInternal

    [Fact]
    public void GetCurrentSigningKeyInternal_should_return_the_oldest_active_key()
    {
        var key1 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(10)));
        var key2 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(5)));
        var key3 = CreateKey(_options.KeyManagement.PropagationTime.Add(-TimeSpan.FromSeconds(5)));
        var key4 = CreateKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(5)));

        var key = _subject.GetCurrentSigningKeyInternal(new[] { key1, key2, key3, key4 });

        key.ShouldNotBeNull();
        key.Id.ShouldBe(key1.Id);
    }

    [Fact]
    public void GetCurrentSigningKeyInternal_should_return_a_matching_key_type()
    {
        var rsaKey1 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(10)));
        var x509Key1 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(20)), x509: true);

        {
            _rsaOptions.UseX509Certificate = false;
            var key = _subject.GetCurrentSigningKeyInternal(new[] { rsaKey1, x509Key1 });

            key.ShouldNotBeNull();
            key.Id.ShouldBe(x509Key1.Id);
        }
        {
            _rsaOptions.UseX509Certificate = true;
            var key = _subject.GetCurrentSigningKeyInternal(new[] { rsaKey1, x509Key1 });

            key.ShouldNotBeNull();
            key.Id.ShouldBe(x509Key1.Id);
        }

        {
            var rsaKey2 = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(30)));

            _rsaOptions.UseX509Certificate = false;
            var key = _subject.GetCurrentSigningKeyInternal(new[] { rsaKey1, x509Key1, rsaKey2 });

            key.ShouldNotBeNull();
            key.Id.ShouldBe(rsaKey2.Id);
        }
    }

    // CanBeUsedForSigning

    [Fact]
    public void CanBeUsedForSigning_key_created_within_activity_delay_should_not_be_used_for_signing()
    {
        {
            var key = CreateKey(-TimeSpan.FromSeconds(1));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeFalse();
        }

        {
            var key = CreateKey(TimeSpan.FromSeconds(1));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeFalse();
        }

        {
            var key = CreateKey(_options.KeyManagement.PropagationTime.Subtract(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeFalse();
        }
    }

    [Fact]
    public void CanBeUsedForSigning_key_created_after_active_delay_should_be_used_for_signing()
    {
        {
            var key = CreateKey(_options.KeyManagement.PropagationTime);

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(_options.KeyManagement.RotationInterval);

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void CanBeUsedForSigning_key_older_than_expiration_should_not_be_used_for_signing()
    {
        {
            var key = CreateKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeFalse();
        }
    }

    [Fact]
    public void CanBeUsedForSigning_ignoring_activity_delay_key_created_within_activity_delay_should_be_used_for_signing()
    {
        {
            var key = CreateKey(-TimeSpan.FromSeconds(1));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key, true);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(TimeSpan.FromSeconds(1));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key, true);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(_options.KeyManagement.PropagationTime.Subtract(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key, true);

            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void CanBeUsedForSigning_ignoring_activity_delay_key_created_after_active_delay_should_be_used_for_signing()
    {
        {
            var key = CreateKey(_options.KeyManagement.PropagationTime);

            var result = _subject.CanBeUsedAsCurrentSigningKey(key, true);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key, true);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key, true);

            result.ShouldBeTrue();
        }

        {
            var key = CreateKey(_options.KeyManagement.RotationInterval);

            var result = _subject.CanBeUsedAsCurrentSigningKey(key);

            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void CanBeUsedForSigning_ignoring_activity_delay_key_older_than_expiration_should_not_be_used_for_signing()
    {
        {
            var key = CreateKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromSeconds(1)));

            var result = _subject.CanBeUsedAsCurrentSigningKey(key, true);

            result.ShouldBeFalse();
        }
    }

    // CreateAndStoreNewKeyAsync

    [Fact]
    public async Task CreateAndStoreNewKeyAsync_should_create_and_store_and_return_key()
    {
        var result = await _subject.CreateAndStoreNewKeyAsync(_rsaOptions);

        _mockKeyProtector.ProtectWasCalled.ShouldBeTrue();
        _mockKeyStore.Keys.Count.ShouldBe(1);
        _mockKeyStore.Keys.Single().Id.ShouldBe(result.Id);
        result.Created.ShouldBe(_mockClock.UtcNow.UtcDateTime);
        result.Algorithm.ShouldBe("RS256");
    }

    // IsKeyRotationRequired

    [Fact]
    public void IsKeyRotationRequired_when_no_keys_should_be_true()
    {
        {
            var result = _subject.IsKeyRotationRequired(null);
            result.ShouldBeTrue();
        }
        {
            var result = _subject.IsKeyRotationRequired(new RsaKeyContainer[0]);
            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void IsKeyRotationRequired_when_no_active_key_should_be_true()
    {
        {
            var keys = new KeyContainer[] {
                CreateKey(_options.KeyManagement.KeyRetirementAge.Add(TimeSpan.FromDays(1))),
            };
            var result = _subject.IsKeyRotationRequired(keys);
            result.ShouldBeTrue();
        }

        {
            var keys = new[] {
                CreateKey(_options.KeyManagement.RotationInterval.Add(TimeSpan.FromDays(1))),
            };

            var result = _subject.IsKeyRotationRequired(keys);
            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void IsKeyRotationRequired_when_active_key_is_not_about_to_expire_should_be_false()
    {
        var keys = new[] {
            CreateKey(_options.KeyManagement.RotationInterval.Subtract(_options.KeyManagement.PropagationTime.Add(TimeSpan.FromSeconds(1)))),
        };

        var result = _subject.IsKeyRotationRequired(keys);
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsKeyRotationRequired_when_active_key_is_about_to_expire_should_be_true()
    {
        {
            var keys = new[] {
                CreateKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1))),
            };

            var result = _subject.IsKeyRotationRequired(keys);
            result.ShouldBeTrue();
        }
        {
            var keys = new[] {
                CreateKey(_options.KeyManagement.RotationInterval.Subtract(_options.KeyManagement.PropagationTime)),
            };

            var result = _subject.IsKeyRotationRequired(keys);
            result.ShouldBeTrue();
        }
    }

    [Fact]
    public void IsKeyRotationRequired_when_younger_keys_exist_should_be_false()
    {
        {
            var keys = new[] {
                CreateKey(_options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1))), // active key about to expire
                CreateKey() // very new key
            };

            var result = _subject.IsKeyRotationRequired(keys);
            result.ShouldBeFalse();
        }
        {
            var keys = new[] {
                CreateKey(_options.KeyManagement.PropagationTime), // active key not about to expire
                CreateKey() // very new key
            };

            var result = _subject.IsKeyRotationRequired(keys);
            result.ShouldBeFalse();
        }
    }

    [Fact]
    public void IsKeyRotationRequired_when_younger_keys_are_close_to_expiration_should_be_true()
    {
        {
            var age = _options.KeyManagement.RotationInterval.Subtract(TimeSpan.FromSeconds(1));
            var keys = new[] {
                CreateKey(age), // active key about to expire
                CreateKey(age.Subtract(TimeSpan.FromSeconds(1)))  // newer, but still close to expiration
            };

            var result = _subject.IsKeyRotationRequired(keys);
            result.ShouldBeTrue();
        }
    }
}
