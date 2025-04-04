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
    private readonly IClock _clock;
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
    /// <param name="clock"></param>
    /// <param name="newKeyLock"></param>
    /// <param name="logger"></param>
    /// <param name="issuerNameService"></param>
    public KeyManager(
        IdentityServerOptions options,
        ISigningKeyStore store,
        ISigningKeyStoreCache cache,
        ISigningKeyProtector protector,
        IClock clock,
        IConcurrencyLock<KeyManager> newKeyLock,
        ILogger<KeyManager> logger,
        IIssuerNameService issuerNameService)
    {
        options.KeyManagement.Validate();

        _options = options;
        _store = store;
        _cache = cache;
        _protector = protector;
        _clock = clock;
        _newKeyLock = newKeyLock;
        _logger = logger;
        _issuerNameService = issuerNameService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<KeyContainer>> GetCurrentKeysAsync()
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("KeyManager.GetCurrentKeys");

        _logger.LogTrace("Getting the current key.");

        var (_, currentKeys) = await GetAllKeysInternalAsync();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var key in currentKeys)
            {
                var age = _clock.GetAge(key.Created);
                var expiresIn = _options.KeyManagement.RotationInterval.Subtract(age);
                var retiresIn = _options.KeyManagement.KeyRetirementAge.Subtract(age);
                _logger.LogInformation("Active signing key found with kid {kid} for alg {alg}. Expires in {KeyExpiration}. Retires in {KeyRetirement}", key.Id, key.Algorithm, expiresIn, retiresIn);
            }
        }

        return currentKeys;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<KeyContainer>> GetAllKeysAsync()
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("KeyManager.GetAllKeys");

        _logger.LogTrace("Getting all the keys.");

        var (keys, _) = await GetAllKeysInternalAsync();
        return keys;
    }



    internal async Task<(IEnumerable<KeyContainer> allKeys, IEnumerable<KeyContainer> signingKeys)> GetAllKeysInternalAsync()
    {
        var cached = true;
        var keys = await GetAllKeysFromCacheAsync();
        if (!keys.Any())
        {
            cached = false;
            keys = await GetAllKeysFromStoreAsync();
        }

        // ensure we have all of our active signing keys
        IEnumerable<KeyContainer> signingKeys;
        var signingKeysSuccess = TryGetAllCurrentSigningKeys(keys, out signingKeys);

        // if we loaded from cache, see if DB has updated key
        if (!signingKeysSuccess && cached)
        {
            _logger.LogTrace("Not all signing keys current in cache, reloading keys from database.");
        }

        var rotationRequired = false;

        // if we don't have an active key, then a new one is about to be created so don't bother running this check
        if (signingKeysSuccess)
        {
            rotationRequired = IsKeyRotationRequired(keys);
            if (rotationRequired && cached)
            {
                _logger.LogTrace("Key rotation required, reloading keys from database.");
            }
        }

        if (!signingKeysSuccess || rotationRequired)
        {
            _logger.LogTrace("Entering new key lock.");

            // need to create new key, but another thread might have already so acquiring lock.
            if (false == await _newKeyLock.LockAsync((int)_options.Caching.CacheLockTimeout.TotalMilliseconds))
            {
                throw new Exception($"Failed to obtain new key lock for: '{GetType()}'");
            }

            try
            {
                // check if another thread did the work already
                keys = await GetAllKeysFromCacheAsync();

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
                    keys = await GetAllKeysFromStoreAsync();

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
                            _logger.LogTrace("No active keys; new key creation required.");
                        }
                        else
                        {
                            _logger.LogTrace("Approaching key retirement; new key creation required.");
                        }

                        // now we know we need to create new keys
                        (keys, signingKeys) = await CreateNewKeysAndAddToCacheAsync();
                    }
                    else
                    {
                        _logger.LogTrace("Another server created new key.");
                    }
                }
                else
                {
                    _logger.LogTrace("Another thread created new key.");
                }
            }
            finally
            {
                _logger.LogTrace("Releasing new key lock.");
                _newKeyLock.Unlock();
            }
        }

        if (!signingKeys.Any())
        {
            _logger.LogError("Failed to create and then load new keys.");
            throw new Exception("Failed to create and then load new keys.");
        }

        return (keys, signingKeys);
    }

    internal bool IsKeyRotationRequired(IEnumerable<KeyContainer> allKeys)
    {
        if (allKeys == null || !allKeys.Any()) return true;

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
            var age = _clock.GetAge(activeKey.Created);
            var diff = _options.KeyManagement.RotationInterval.Subtract(age);
            var needed = (diff <= _options.KeyManagement.PropagationTime);

            if (!needed)
            {
                _logger.LogTrace("Key rotation not required for alg {alg}; New key expected to be created in {KeyRotiation}", item.Key, diff.Subtract(_options.KeyManagement.PropagationTime));
            }
            else
            {
                _logger.LogTrace("Key rotation required now for alg {alg}.", item.Key);
                return true;
            }
        }

        return false;
    }

    internal async Task<KeyContainer> CreateAndStoreNewKeyAsync(SigningAlgorithmOptions alg)
    {
        _logger.LogTrace("Creating new key.");

        var now = _clock.UtcNow.UtcDateTime;
        var iss = await _issuerNameService.GetCurrentAsync();

        KeyContainer container;

        if (alg.IsRsaKey)
        {
            var rsa = CryptoHelper.CreateRsaSecurityKey(_options.KeyManagement.RsaKeySize);

            container = alg.UseX509Certificate ?
                new X509KeyContainer(rsa, alg.Name, now, _options.KeyManagement.KeyRetirementAge, iss) :
                new RsaKeyContainer(rsa, alg.Name, now);
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
        await _store.StoreKeyAsync(key);

        _logger.LogDebug("Created and stored new key with kid {kid}.", container.Id);

        return container;
    }

    internal async Task<IEnumerable<KeyContainer>> GetAllKeysFromCacheAsync()
    {
        var cachedKeys = await _cache.GetKeysAsync();
        if (cachedKeys != null)
        {
            _logger.LogTrace("Cache hit when loading all keys.");
            return cachedKeys;
        }

        _logger.LogTrace("Cache miss when loading all keys.");
        return Enumerable.Empty<KeyContainer>();
    }

    internal bool AreAllKeysWithinInitializationDuration(IEnumerable<KeyContainer> keys)
    {
        if (_options.KeyManagement.InitializationDuration == TimeSpan.Zero)
        {
            return false;
        }

        // the expired check will also filter retired keys
        keys = FilterExpiredKeys(keys);

        var result = keys.All(x =>
        {
            var age = _clock.GetAge(x.Created);
            var isNew = _options.KeyManagement.IsWithinInitializationDuration(age);
            return isNew;
        });

        return result;
    }

    internal async Task<IEnumerable<SerializedKey>> FilterAndDeleteRetiredKeysAsync(IEnumerable<SerializedKey> keys)
    {
        var retired = keys
            .Where(x =>
            {
                return (x != null) &&
                    _options.KeyManagement.IsRetired(_clock.GetAge(x.Created));
            })
            .ToArray();

        if (retired.Any())
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var ids = retired.Select(x => x.Id).ToArray();
                _logger.LogTrace("Filtered retired keys from store: {kids}", ids.Aggregate((x, y) => $"{x},{y}"));
            }

            if (_options.KeyManagement.DeleteRetiredKeys)
            {
                var ids = retired.Select(x => x.Id).ToArray();
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Deleting retired keys from store: {kids}", ids.Aggregate((x, y) => $"{x},{y}"));
                }
                await DeleteKeysAsync(ids);
            }
        }

        var result = keys.Except(retired).ToArray();
        return result;
    }

    internal async Task DeleteKeysAsync(IEnumerable<string> keys)
    {
        if (keys == null || !keys.Any()) return;

        foreach (var key in keys)
        {
            await _store.DeleteKeyAsync(key);
        }
    }

    internal IEnumerable<KeyContainer> FilterExpiredKeys(IEnumerable<KeyContainer> keys)
    {
        var result = keys
            .Where(x =>
            {
                var age = _clock.GetAge(x.Created);
                var isExpired = _options.KeyManagement.IsExpired(age);
                return !isExpired;
            });

        return result;
    }

    internal async Task CacheKeysAsync(IEnumerable<KeyContainer> keys)
    {
        if (keys?.Any() == true)
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
                    _logger.LogTrace("Caching keys with InitializationKeyCacheDuration for {InitializationKeyCacheDuration}", _options.KeyManagement.InitializationKeyCacheDuration);
                }
            }
            else if (_options.KeyManagement.KeyCacheDuration > TimeSpan.Zero)
            {
                _logger.LogTrace("Caching keys with KeyCacheDuration for {KeyCacheDuration}", _options.KeyManagement.KeyCacheDuration);
            }

            if (duration > TimeSpan.Zero)
            {
                await _cache.StoreKeysAsync(keys, duration);
            }
        }
    }

    internal async Task<IEnumerable<KeyContainer>> GetAllKeysFromStoreAsync(bool cache = true)
    {
        _logger.LogTrace("Loading keys from store.");

        var protectedKeys = await _store.LoadKeysAsync();
        if (protectedKeys != null && protectedKeys.Any())
        {
            // retired keys are those that are beyond inclusion, thus we act as if they don't exist.
            protectedKeys = await FilterAndDeleteRetiredKeysAsync(protectedKeys);

            var keys = protectedKeys.Select(x =>
                {
                    try
                    {
                        var key = _protector.Unprotect(x);
                        if (key == null)
                        {
                            _logger.LogWarning("Key with kid {kid} failed to unprotect.", x.Id);
                        }
                        return key;
                    }
                    catch (CryptographicException ex)
                    {
                        _logger.LogError(ex, "Error unprotecting the IdentityServer signing key with kid {kid}. This is likely due to the ASP.NET Core data protection key that was used to protect it is not available. This could occur because data protection has not been configured properly for your load balanced environment, or the IdentityServer signing key store was populated with keys from a different environment with different ASP.NET Core data protection keys. Once you have corrected the problem and if you keep getting this error then it is safe to delete the specific IdentityServer signing key with that kid.", x?.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading key with kid {kid}.", x?.Id);
                    }
                    return null;
                })
                .Where(x => x != null)
                .ToArray()
                .AsEnumerable();

            if (_logger.IsEnabled(LogLevel.Trace) && keys.Any())
            {
                var ids = keys.Select(x => x.Id).ToArray();
                _logger.LogTrace("Loaded keys from store: {kids}", ids.Aggregate((x, y) => $"{x},{y}"));
            }


            if (_logger.IsEnabled(LogLevel.Trace) && keys.Any())
            {
                var ids = keys.Select(x => x.Id).ToArray();
                _logger.LogTrace("Remaining keys after filter: {kids}", ids.Aggregate((x, y) => $"{x},{y}"));
            }

            // only use keys that are allowed
            keys = keys.Where(x => _options.KeyManagement.AllowedSigningAlgorithmNames.Contains(x.Algorithm)).ToArray();
            if (_logger.IsEnabled(LogLevel.Trace) && keys.Any())
            {
                var ids = keys.Select(x => x.Id).ToArray();
                _logger.LogTrace("Keys with allowed alg from store: {kids}", ids.Aggregate((x, y) => $"{x},{y}"));
            }

            if (keys.Any())
            {
                _logger.LogTrace("Keys successfully returned from store.");

                if (cache)
                {
                    await CacheKeysAsync(keys);
                }

                return keys;
            }
        }

        _logger.LogTrace("No keys returned from store.");

        return Enumerable.Empty<KeyContainer>();
    }




    internal async Task<(IEnumerable<KeyContainer> allKeys, IEnumerable<KeyContainer> activeKeys)> CreateNewKeysAndAddToCacheAsync()
    {
        var keys = new List<KeyContainer>();
        keys.AddRange(await _cache.GetKeysAsync() ?? Enumerable.Empty<KeyContainer>());

        foreach (var alg in _options.KeyManagement.SigningAlgorithms)
        {
            var newKey = await CreateAndStoreNewKeyAsync(alg);
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
                _logger.LogTrace("All keys are new; delaying before reloading keys from store by InitializationSynchronizationDelay for {InitializationSynchronizationDelay}.", _options.KeyManagement.InitializationSynchronizationDelay);
                await Task.Delay(_options.KeyManagement.InitializationSynchronizationDelay);
            }
            else
            {
                _logger.LogTrace("All keys are new; reloading keys from store.");
            }

            // reload in case other new keys were recently created
            keys = new List<KeyContainer>(await GetAllKeysFromStoreAsync(false));
        }

        // explicitly cache here since we didn't when we loaded above
        await CacheKeysAsync(keys);

        var activeKeys = GetAllCurrentSigningKeys(keys);

        return (keys, activeKeys);
    }

    internal bool TryGetAllCurrentSigningKeys(IEnumerable<KeyContainer> keys, out IEnumerable<KeyContainer> signingKeys)
    {
        signingKeys = GetAllCurrentSigningKeys(keys);

        var success = signingKeys.Count() == _options.KeyManagement.AllowedSigningAlgorithmNames.Count() &&
                      signingKeys.All(x => _options.KeyManagement.AllowedSigningAlgorithmNames.Contains(x.Algorithm));

        return success;
    }

    internal IEnumerable<KeyContainer> GetAllCurrentSigningKeys(IEnumerable<KeyContainer> allKeys)
    {
        if (allKeys == null || !allKeys.Any())
        {
            return Enumerable.Empty<KeyContainer>();
        }

        _logger.LogTrace("Looking for active signing keys.");

        var list = new List<KeyContainer>();
        var groupedKeys = allKeys.GroupBy(x => x.Algorithm);
        foreach (var item in groupedKeys)
        {
            _logger.LogTrace("Looking for an active signing key for alg {alg}.", item.Key);

            var activeKey = GetCurrentSigningKey(item);
            if (activeKey != null)
            {
                _logger.LogTrace("Found active signing key for alg {alg} with kid {kid}.", item.Key, activeKey.Id);
                list.Add(activeKey);
            }
            else
            {
                _logger.LogTrace("Failed to find active signing key for alg {alg}.", item.Key);
            }
        }

        return list;
    }

    internal KeyContainer GetCurrentSigningKey(IEnumerable<KeyContainer> keys)
    {
        if (keys == null || !keys.Any()) return null;

        var ignoreActivation = false;
        // look for keys past activity delay
        var activeKey = GetCurrentSigningKeyInternal(keys, ignoreActivation);
        if (activeKey == null)
        {
            ignoreActivation = true;
            _logger.LogTrace("No active signing key found (respecting the activation delay).");

            // none, so check if any of the keys were recently created
            activeKey = GetCurrentSigningKeyInternal(keys, ignoreActivation);

            if (activeKey == null)
            {
                _logger.LogTrace("No active signing key found (ignoring the activation delay).");
            }
        }

        if (activeKey != null && _logger.IsEnabled(LogLevel.Debug))
        {
            var delay = ignoreActivation ? "(ignoring the activation delay)" : "(respecting the activation delay)";
            _logger.LogTrace("Active signing key found " + delay + " with kid: {kid}.", activeKey.Id);
        }

        return activeKey;
    }

    internal KeyContainer GetCurrentSigningKeyInternal(IEnumerable<KeyContainer> keys, bool ignoreActivationDelay = false)
    {
        if (keys == null) return null;

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
        if (key == null) return false;

        var alg = _options.KeyManagement.SigningAlgorithms.SingleOrDefault(x => x.Name == key.Algorithm);
        if (alg == null)
        {
            _logger.LogTrace("Key {kid} signing algorithm {alg} not allowed by server options.", key.Id, key.Algorithm);
            return false;
        }

        if (alg.UseX509Certificate && !key.HasX509Certificate)
        {
            _logger.LogTrace("Server configured to wrap keys in X509 certs, but key {kid} is not wrapped in cert.", key.Id);
            return false;
        }

        var now = _clock.UtcNow.UtcDateTime;

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
            _logger.LogTrace("Checking if key with kid {kid} is active (respecting activation delay).", key.Id);
            start = start.Add(_options.KeyManagement.PropagationTime);
        }
        else
        {
            _logger.LogTrace("Checking if key with kid {kid} is active (ignoring activation delay).", key.Id);
        }

        if (start > now)
        {
            _logger.LogTrace("Key with kid {kid} is inactive: the current time is prior to its activation delay.", key.Id);
            return false;
        }

        // expired key check
        var end = key.Created.Add(_options.KeyManagement.RotationInterval);
        if (end < now)
        {
            _logger.LogTrace("Key with kid {kid} is inactive: the current time is past its expiration.", key.Id);
            return false;
        }

        _logger.LogTrace("Key with kid {kid} is active.", key.Id);

        return true;
    }
}
