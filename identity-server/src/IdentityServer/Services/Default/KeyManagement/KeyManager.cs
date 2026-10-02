// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Implementation of IKeyManager that creates, stores, and rotates signing keys.
/// </summary>
public class KeyManager : IKeyManager
{
    private readonly IdentityServerOptions _options;
    private readonly ISigningKeyStore _store;
    private readonly ISigningKeyStoreCache _cache;
    private readonly ISigningKeyProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly IConcurrencyLock<KeyManager> _newKeyLock;
    private readonly ILogger<KeyManager> _logger;
    private readonly IIssuerNameService _issuerNameService;

    /// <summary>
    /// Constructor for KeyManager
    /// </summary>
    /// <param name="options"></param>
    /// <param name="store"></param>
    /// <param name="cache"></param>
    /// <param name="protector"></param>
    /// <param name="timeProvider"></param>
    /// <param name="newKeyLock"></param>
    /// <param name="logger"></param>
    /// <param name="issuerNameService"></param>
    public KeyManager(
        IdentityServerOptions options,
        ISigningKeyStore store,
        ISigningKeyStoreCache cache,
        ISigningKeyProtector protector,
        TimeProvider timeProvider,
        IConcurrencyLock<KeyManager> newKeyLock,
        ILogger<KeyManager> logger,
        IIssuerNameService issuerNameService)
    {
        options.KeyManagement.Validate();

        _options = options;
        _store = store;
        _cache = cache;
        _protector = protector;
        _timeProvider = timeProvider;
        _newKeyLock = newKeyLock;
        _logger = logger;
        _issuerNameService = issuerNameService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<KeyContainer>> GetCurrentKeysAsync(Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("KeyManager.GetCurrentKeys");

        _logger.GettingTheCurrentKey();

        var (_, currentKeys) = await GetAllKeysInternalAsync(ct);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var key in currentKeys)
            {
                var age = _timeProvider.GetAge(key.Created);
                var expiresIn = _options.KeyManagement.RotationInterval.Subtract(age);
                var retiresIn = _options.KeyManagement.KeyRetirementAge.Subtract(age);
                _logger.ActiveSigningKeyFoundWithKidKidFor(key.Id, key.Algorithm, expiresIn, retiresIn);
            }
        }

        return currentKeys;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<KeyContainer>> GetAllKeysAsync(Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("KeyManager.GetAllKeys");

        _logger.GettingAllTheKeys();

        var (keys, _) = await GetAllKeysInternalAsync(ct);
        return keys;
    }



    internal async Task<(IReadOnlyCollection<KeyContainer> allKeys, IReadOnlyCollection<KeyContainer> signingKeys)> GetAllKeysInternalAsync(Ct ct)
    {
        var cached = true;
        var keys = await GetAllKeysFromCacheAsync(ct);
        if (keys.Count == 0)
        {
            cached = false;
            keys = await GetAllKeysFromStoreAsync(ct);
        }

        // ensure we have all of our active signing keys
        IReadOnlyCollection<KeyContainer> signingKeys;
        var signingKeysSuccess = TryGetAllCurrentSigningKeys(keys, out signingKeys);

        // if we loaded from cache, see if DB has updated key
        if (!signingKeysSuccess && cached)
        {
            _logger.NotAllSigningKeysCurrentInCacheReloading();
        }

        var rotationRequired = false;

        // if we don't have an active key, then a new one is about to be created so don't bother running this check
        if (signingKeysSuccess)
        {
            rotationRequired = IsKeyRotationRequired(keys);
            if (rotationRequired && cached)
            {
                _logger.KeyRotationRequiredReloadingKeysFromDatabase();
            }
        }

        if (!signingKeysSuccess || rotationRequired)
        {
            _logger.EnteringNewKeyLock();

            // need to create new key, but another thread might have already so acquiring lock.
#pragma warning disable CS0618 // CacheLockTimeout is obsolete but still used by KeyManager for IConcurrencyLock
            if (false == await _newKeyLock.LockAsync((int)_options.Caching.CacheLockTimeout.TotalMilliseconds))
#pragma warning restore CS0618
            {
                throw new Exception($"Failed to obtain new key lock for: '{GetType()}'");
            }

            try
            {
                // check if another thread did the work already
                keys = await GetAllKeysFromCacheAsync(ct);

                if (!signingKeysSuccess)
                {
                    signingKeysSuccess = TryGetAllCurrentSigningKeys(keys, out signingKeys);
                }
                if (rotationRequired)
                {
                    rotationRequired = IsKeyRotationRequired(keys);
                }

                if (!signingKeysSuccess || rotationRequired)
                {
                    // still need to do the work, but check if another server did the work already
                    keys = await GetAllKeysFromStoreAsync(ct);

                    if (!signingKeysSuccess)
                    {
                        signingKeysSuccess = TryGetAllCurrentSigningKeys(keys, out signingKeys);
                    }
                    if (rotationRequired)
                    {
                        rotationRequired = IsKeyRotationRequired(keys);
                    }

                    if (!signingKeysSuccess || rotationRequired)
                    {
                        if (!signingKeysSuccess)
                        {
                            _logger.NoActiveKeysNewKeyCreationRequired();
                        }
                        else
                        {
                            _logger.ApproachingKeyRetirementNewKeyCreationRequired();
                        }

                        // now we know we need to create new keys
                        (keys, signingKeys) = await CreateNewKeysAndAddToCacheAsync(ct);
                    }
                    else
                    {
                        _logger.AnotherServerCreatedNewKey();
                    }
                }
                else
                {
                    _logger.AnotherThreadCreatedNewKey();
                }
            }
            finally
            {
                _logger.ReleasingNewKeyLock();
                _newKeyLock.Unlock();
            }
        }

        if (signingKeys.Count == 0)
        {
            _logger.FailedToCreateAndThenLoadNewKeys();
            throw new Exception("Failed to create and then load new keys.");
        }

        return (keys, signingKeys);
    }

    internal bool IsKeyRotationRequired(IReadOnlyCollection<KeyContainer> allKeys)
    {
        if (allKeys == null || allKeys.Count == 0)
        {
            return true;
        }

        var groupedKeys = allKeys.GroupBy(x => x.Algorithm).ToArray();

        var success = groupedKeys.Length == _options.KeyManagement.AllowedSigningAlgorithmNames.Count() &&
                      groupedKeys.All(x => _options.KeyManagement.AllowedSigningAlgorithmNames.Contains(x.Key));

        if (!success)
        {
            return true;
        }

        foreach (var item in groupedKeys)
        {
            var keys = item.AsEnumerable();
            var activeKey = GetCurrentSigningKey(keys);

            if (activeKey == null)
            {
                return true;
            }

            // rotation is needed if: 1) if there are no other keys next in line (meaning younger).
            // and 2) the current activation key is near expiration (using the delay timeout)

            // get younger keys (which will also filter active key)
            keys = keys.Where(x => x.Created > activeKey.Created).ToArray();

            if (keys.Any())
            {
                // there are younger keys, then they might also be within the window of the key activation delay
                // so find the youngest one and treat that one as if it's the active key.
                activeKey = keys.MaxBy(x => x.Created);
            }

            // if no younger keys, then check if we're nearing the expiration of active key
            // and see if that's within the window of activation delay.
            var age = _timeProvider.GetAge(activeKey.Created);
            var diff = _options.KeyManagement.RotationInterval.Subtract(age);
            var needed = (diff <= _options.KeyManagement.PropagationTime);

            if (!needed)
            {
                _logger.KeyRotationNotRequiredForAlgAlgNew(item.Key, diff.Subtract(_options.KeyManagement.PropagationTime));
            }
            else
            {
                _logger.KeyRotationRequiredNowForAlgAlg(item.Key);
                return true;
            }
        }

        return false;
    }

    internal async Task<KeyContainer> CreateAndStoreNewKeyAsync(SigningAlgorithmOptions alg, Ct ct)
    {
        _logger.CreatingNewKey();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        KeyContainer container;

        if (alg.IsRsaKey)
        {
            var rsa = CryptoHelper.CreateRsaSecurityKey(_options.KeyManagement.RsaKeySize);

            if (alg.UseX509Certificate)
            {
                var iss = await _issuerNameService.GetCurrentAsync(ct);
                container = new X509KeyContainer(rsa, alg.Name, now, _options.KeyManagement.KeyRetirementAge, iss);
            }
            else
            {
                container = new RsaKeyContainer(rsa, alg.Name, now);
            }
        }
        else if (alg.IsEcKey)
        {
            var ec = CryptoHelper.CreateECDsaSecurityKey(CryptoHelper.GetCurveNameFromSigningAlgorithm(alg.Name));
            // X509 certs don't currently work with EC keys.
            container = //_options.KeyManagement.WrapKeysInX509Certificate ? //new X509KeyContainer(ec, alg, now, _options.KeyManagement.KeyRetirementAge, iss) :
                new EcKeyContainer(ec, alg.Name, now);
        }
        else
        {
            throw new Exception($"Invalid alg '{alg}'");
        }

        var key = _protector.Protect(container);
        await _store.StoreKeyAsync(key, ct);

        _logger.CreatedAndStoredNewKeyWithKidKid(container.Id);

        return container;
    }

    internal async Task<IReadOnlyCollection<KeyContainer>> GetAllKeysFromCacheAsync(Ct ct)
    {
        var cachedKeys = await _cache.GetKeysAsync(ct);
        if (cachedKeys != null)
        {
            _logger.CacheHitWhenLoadingAllKeys();
            return cachedKeys;
        }

        _logger.CacheMissWhenLoadingAllKeys();
        return Array.Empty<KeyContainer>();
    }

    internal bool AreAllKeysWithinInitializationDuration(IReadOnlyCollection<KeyContainer> keys)
    {
        if (_options.KeyManagement.InitializationDuration == TimeSpan.Zero)
        {
            return false;
        }

        // the expired check will also filter retired keys
        keys = FilterExpiredKeys(keys);

        var result = keys.All(x =>
        {
            var age = _timeProvider.GetAge(x.Created);
            var isNew = _options.KeyManagement.IsWithinInitializationDuration(age);
            return isNew;
        });

        return result;
    }

    internal async Task<IReadOnlyCollection<SerializedKey>> FilterAndDeleteRetiredKeysAsync(IReadOnlyCollection<SerializedKey> keys, Ct ct)
    {
        var retired = keys
            .Where(x =>
            {
                return (x != null) &&
                    _options.KeyManagement.IsRetired(_timeProvider.GetAge(x.Created));
            })
            .ToArray();

        if (retired.Length > 0)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var ids = retired.Select(x => x.Id).ToArray();
                _logger.FilteredRetiredKeysFromStoreKids(ids.Aggregate((x, y) => $"{x},{y}"));
            }

            if (_options.KeyManagement.DeleteRetiredKeys)
            {
                var ids = retired.Select(x => x.Id).ToArray();
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.DeletingRetiredKeysFromStoreKids(ids.Aggregate((x, y) => $"{x},{y}"));
                }
                await DeleteKeysAsync(ids, ct);
            }
        }

        var result = keys.Except(retired).ToArray();
        return result;
    }

    internal async Task DeleteKeysAsync(IReadOnlyCollection<string> keys, Ct ct)
    {
        if (keys == null || keys.Count == 0)
        {
            return;
        }

        foreach (var key in keys)
        {
            await _store.DeleteKeyAsync(key, ct);
        }
    }

    internal IReadOnlyCollection<KeyContainer> FilterExpiredKeys(IReadOnlyCollection<KeyContainer> keys)
    {
        var result = keys
            .Where(x =>
            {
                var age = _timeProvider.GetAge(x.Created);
                var isExpired = _options.KeyManagement.IsExpired(age);
                return !isExpired;
            })
            .ToArray();

        return result;
    }

    internal async Task CacheKeysAsync(IReadOnlyCollection<KeyContainer> keys, Ct ct)
    {
        if (keys?.Count > 0)
        {
            var duration = _options.KeyManagement.KeyCacheDuration;

            if (AreAllKeysWithinInitializationDuration(keys))
            {
                // if all key are new, then we want to use the shorter initialization key cache duration.
                // this attempts to allow other servers that are slow to write new keys to complete, then we will
                // have the most up to date keys in the cache sooner.
                duration = _options.KeyManagement.InitializationKeyCacheDuration;
                if (duration > TimeSpan.Zero)
                {
                    _logger.CachingKeysWithInitializationKeyCacheDurationForInitializationKeyCacheDuration(_options.KeyManagement.InitializationKeyCacheDuration);
                }
            }
            else if (_options.KeyManagement.KeyCacheDuration > TimeSpan.Zero)
            {
                _logger.CachingKeysWithKeyCacheDurationForKeyCacheDuration(_options.KeyManagement.KeyCacheDuration);
            }

            if (duration > TimeSpan.Zero)
            {
                await _cache.StoreKeysAsync(keys, duration, ct);
            }
        }
    }

    internal async Task<IReadOnlyCollection<KeyContainer>> GetAllKeysFromStoreAsync(Ct ct, bool cache = true)
    {
        _logger.LoadingKeysFromStore();

        var protectedKeys = await _store.LoadKeysAsync(ct);
        if (protectedKeys != null && protectedKeys.Count > 0)
        {
            // retired keys are those that are beyond inclusion, thus we act as if they don't exist.
            var filteredKeys = await FilterAndDeleteRetiredKeysAsync(protectedKeys, ct);

            var keys = filteredKeys.Select(x =>
                {
                    try
                    {
                        var key = _protector.Unprotect(x);
                        if (key == null)
                        {
                            _logger.KeyWithKidKidFailedToUnprotect(x.Id);
                        }
                        return key;
                    }
                    catch (CryptographicException ex)
                    {
                        _logger.ErrorUnprotectingTheIdentityServerSigningKeyWithKid(ex, x?.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorLoadingKeyWithKidKid(ex, x?.Id);
                    }
                    return null;
                })
                .Where(x => x != null)
                .ToArray();

            if (_logger.IsEnabled(LogLevel.Trace) && keys.Length > 0)
            {
                var ids = keys.Select(x => x.Id).ToArray();
                _logger.LoadedKeysFromStoreKids(ids.Aggregate((x, y) => $"{x},{y}"));
            }


            if (_logger.IsEnabled(LogLevel.Trace) && keys.Length > 0)
            {
                var ids = keys.Select(x => x.Id).ToArray();
                _logger.RemainingKeysAfterFilterKids(ids.Aggregate((x, y) => $"{x},{y}"));
            }

            // only use keys that are allowed
            var allowedKeys = keys.Where(x => _options.KeyManagement.AllowedSigningAlgorithmNames.Contains(x.Algorithm)).ToArray();
            if (_logger.IsEnabled(LogLevel.Trace) && allowedKeys.Length > 0)
            {
                var ids = allowedKeys.Select(x => x.Id).ToArray();
                _logger.KeysWithAllowedAlgFromStoreKids(ids.Aggregate((x, y) => $"{x},{y}"));
            }

            if (allowedKeys.Length > 0)
            {
                _logger.KeysSuccessfullyReturnedFromStore();

                if (cache)
                {
                    await CacheKeysAsync(allowedKeys, ct);
                }

                return allowedKeys;
            }
        }

        _logger.NoKeysReturnedFromStore();

        return Array.Empty<KeyContainer>();
    }




    internal async Task<(IReadOnlyCollection<KeyContainer> allKeys, IReadOnlyCollection<KeyContainer> activeKeys)> CreateNewKeysAndAddToCacheAsync(Ct ct)
    {
        var keys = new List<KeyContainer>();
        keys.AddRange(await _cache.GetKeysAsync(ct) ?? Array.Empty<KeyContainer>());

        foreach (var alg in _options.KeyManagement.SigningAlgorithms)
        {
            var newKey = await CreateAndStoreNewKeyAsync(alg, ct);
            keys.Add(newKey);
        }

        if (AreAllKeysWithinInitializationDuration(keys))
        {
            // this is meant to allow multiple servers that all start at the same time to have some
            // time to complete writing their newly created keys to the store. then when all load
            // each other's keys, they should all agree on the oldest key based on created time.
            // it's intended to address the scenario where two servers start, server1 creates a key whose
            // time is earlier than server2, but server1 is slow to write the key to the store.
            // we don't want server2 to only see server2's key, as it's newer.
            if (_options.KeyManagement.InitializationSynchronizationDelay > TimeSpan.Zero)
            {
                _logger.AllKeysAreNewDelayingBeforeReloadingKeys(_options.KeyManagement.InitializationSynchronizationDelay);
                await Task.Delay(_options.KeyManagement.InitializationSynchronizationDelay, ct);
            }
            else
            {
                _logger.AllKeysAreNewReloadingKeysFromStore();
            }

            // reload in case other new keys were recently created
            keys = new List<KeyContainer>(await GetAllKeysFromStoreAsync(ct, false));
        }

        // explicitly cache here since we didn't when we loaded above
        await CacheKeysAsync(keys, ct);

        var activeKeys = GetAllCurrentSigningKeys(keys);

        return (keys, activeKeys);
    }

    internal bool TryGetAllCurrentSigningKeys(IReadOnlyCollection<KeyContainer> keys, out IReadOnlyCollection<KeyContainer> signingKeys)
    {
        signingKeys = GetAllCurrentSigningKeys(keys);

        var success = signingKeys.Count == _options.KeyManagement.AllowedSigningAlgorithmNames.Count() &&
                      signingKeys.All(x => _options.KeyManagement.AllowedSigningAlgorithmNames.Contains(x.Algorithm));

        return success;
    }

    internal IReadOnlyCollection<KeyContainer> GetAllCurrentSigningKeys(IReadOnlyCollection<KeyContainer> allKeys)
    {
        if (allKeys == null || allKeys.Count == 0)
        {
            return Array.Empty<KeyContainer>();
        }

        _logger.LookingForActiveSigningKeys();

        var list = new List<KeyContainer>();
        var groupedKeys = allKeys.GroupBy(x => x.Algorithm);
        foreach (var item in groupedKeys)
        {
            _logger.LookingForAnActiveSigningKeyForAlg(item.Key);

            var activeKey = GetCurrentSigningKey(item);
            if (activeKey != null)
            {
                _logger.FoundActiveSigningKeyForAlgAlgWith(item.Key, activeKey.Id);
                list.Add(activeKey);
            }
            else
            {
                _logger.FailedToFindActiveSigningKeyForAlg(item.Key);
            }
        }

        return list;
    }

    internal KeyContainer GetCurrentSigningKey(IEnumerable<KeyContainer> keys)
    {
        if (keys == null || !keys.Any())
        {
            return null;
        }

        var ignoreActivation = false;
        // look for keys past activity delay
        var activeKey = GetCurrentSigningKeyInternal(keys, ignoreActivation);
        if (activeKey == null)
        {
            ignoreActivation = true;
            _logger.NoActiveSigningKeyFoundRespectingTheActivation();

            // none, so check if any of the keys were recently created
            activeKey = GetCurrentSigningKeyInternal(keys, ignoreActivation);

            if (activeKey == null)
            {
                _logger.NoActiveSigningKeyFoundIgnoringTheActivation();
            }
        }

        if (activeKey != null && _logger.IsEnabled(LogLevel.Debug))
        {
            var delay = ignoreActivation ? "(ignoring the activation delay)" : "(respecting the activation delay)";
            _logger.ActiveSigningKeyFoundDelayWithKidKid(delay, activeKey.Id);
        }

        return activeKey;
    }

    internal KeyContainer GetCurrentSigningKeyInternal(IEnumerable<KeyContainer> keys, bool ignoreActivationDelay = false)
    {
        if (keys == null)
        {
            return null;
        }

        keys = keys.Where(key => CanBeUsedAsCurrentSigningKey(key, ignoreActivationDelay)).ToArray();
        if (!keys.Any())
        {
            return null;
        }

        // we order by the created date, in essence loading the oldest key
        // this accommodates the scenario where 2 servers create keys at the same time
        // but the first server only reloads the one key it created (and only has the one key for
        // discovery). we don't want the second server using a key that's not in the first server's
        // discovery document. this will be somewhat mitigated by the initial duration where we
        // deliberately ignore the cache.
        var result = keys.MinBy(x => x.Created);
        return result;
    }

    internal bool CanBeUsedAsCurrentSigningKey(KeyContainer key, bool ignoreActiveDelay = false)
    {
        if (key == null)
        {
            return false;
        }

        var alg = _options.KeyManagement.SigningAlgorithms.SingleOrDefault(x => x.Name == key.Algorithm);
        if (alg == null)
        {
            _logger.KeyKidSigningAlgorithmAlgNotAllowedBy(key.Id, key.Algorithm);
            return false;
        }

        if (alg.UseX509Certificate && !key.HasX509Certificate)
        {
            _logger.ServerConfiguredToWrapKeysInX509Certs(key.Id);
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // newly created key check
        var start = key.Created;
        if (start > now)
        {
            // if another server created the key in the future (meaning this server's clock is
            // behind the other), then we will just assume the other server's time for this key.
            // this is how we can deal with clock skew for recently created keys.
            now = start;
        }

        if (!ignoreActiveDelay)
        {
            _logger.CheckingIfKeyWithKidKidIsActive(key.Id);
            start = start.Add(_options.KeyManagement.PropagationTime);
        }
        else
        {
            _logger.CheckingIfKeyWithKidKidIsActive2(key.Id);
        }

        if (start > now)
        {
            _logger.KeyWithKidKidIsInactiveTheCurrent(key.Id);
            return false;
        }

        // expired key check
        var end = key.Created.Add(_options.KeyManagement.RotationInterval);
        if (end < now)
        {
            _logger.KeyWithKidKidIsInactiveTheCurrent2(key.Id);
            return false;
        }

        _logger.KeyWithKidKidIsActive(key.Id);

        return true;
    }
}
