// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Distributed;

namespace Duende.IdentityServer.Services;

/// <summary>
/// The default device flow throttling service using IDistributedCache.
/// </summary>
/// <seealso cref="IDeviceFlowThrottlingService" />
public class DistributedDeviceFlowThrottlingService : IDeviceFlowThrottlingService
{
    private readonly IDistributedCache _cache;
    private readonly IClientStore _clientStore;
    private readonly TimeProvider _timeProvider;
    private readonly IdentityServerOptions _options;

    private const string KeyPrefix = "devicecode_";

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedDeviceFlowThrottlingService"/> class.
    /// </summary>
    /// <param name="cache">The cache.</param>
    /// <param name="clientStore"></param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="options">The options.</param>
    public DistributedDeviceFlowThrottlingService(
        IDistributedCache cache,
        IClientStore clientStore,
        TimeProvider timeProvider,
        IdentityServerOptions options)
    {
        _cache = cache;
        _clientStore = clientStore;
        _timeProvider = timeProvider;
        _options = options;
    }

    /// <summary>
    /// Decides if the requesting client and device code needs to slow down.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    /// <param name="details">The device code details.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">deviceCode</exception>
    public async Task<bool> ShouldSlowDown(string deviceCode, DeviceCode details)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DistributedDeviceFlowThrottlingService.ShouldSlowDown");

        ArgumentNullException.ThrowIfNull(deviceCode);

        var key = KeyPrefix + deviceCode;
        var options = new DistributedCacheEntryOptions { AbsoluteExpiration = _timeProvider.GetUtcNow().AddSeconds(details.Lifetime) };

        var lastSeenAsString = await _cache.GetStringAsync(key);

        // record new
        if (lastSeenAsString == null)
        {
            await _cache.SetStringAsync(key, _timeProvider.GetUtcNow().ToString("O"), options);
            return false;
        }

        // check interval
        if (DateTime.TryParse(lastSeenAsString, out var lastSeen))
        {
            lastSeen = lastSeen.ToUniversalTime();

            var client = await _clientStore.FindEnabledClientByIdAsync(details.ClientId);
            var interval = client?.PollingInterval ?? _options.DeviceFlow.Interval;
            if (_timeProvider.GetUtcNow().UtcDateTime < lastSeen.AddSeconds(interval))
            {
                await _cache.SetStringAsync(key, _timeProvider.GetUtcNow().ToString("O"), options);
                return true;
            }
        }

        // store current and continue
        await _cache.SetStringAsync(key, _timeProvider.GetUtcNow().ToString("O"), options);
        return false;
    }
}
