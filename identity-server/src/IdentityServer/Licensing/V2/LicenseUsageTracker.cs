// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.Private.Licencing.V2;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.Licensing.V2;

internal class LicenseUsageTracker(V2License license)
{
    /// <summary>
    /// Creates an unconfigured tracker for testing purposes.
    /// </summary>
    internal static LicenseUsageTracker CreateForTests() =>
        new(new V2LicenseAccessor(static () => null, NullLogger<V2LicenseAccessor>.Instance).Current);

    private readonly ConcurrentHashSet<string> _featuresUsed = new();
    private readonly ConcurrentHashSet<string> _clientsUsed = new();
    private readonly ConcurrentHashSet<string> _issuersUsed = new();

    public void DPoPUsed() => _featuresUsed.Add(SkuIds.PTC_006);

    public void ResourceIsolationUsed() => _featuresUsed.Add(SkuIds.IS_001);

    public void CibaUsed() => _featuresUsed.Add(SkuIds.PTC_022);

    public void ParUsed() => _featuresUsed.Add(SkuIds.PTC_004);

    public void DynamicProvidersUsed() => _featuresUsed.Add(SkuIds.PLT_005);

    public void ServerSideSessionsUsed() => _featuresUsed.Add(SkuIds.PLT_021);

    public void KeyManagementUsed() => _featuresUsed.Add(SkuIds.PLT_004);

    public void ResourceIndicatorUsed(string? resourceIndicator)
    {
        if (!string.IsNullOrWhiteSpace(resourceIndicator))
        {
            _featuresUsed.Add(SkuIds.IS_001);
        }
    }

    public void ResourceIndicatorsUsed(IEnumerable<string> resourceIndicators)
    {
        if (resourceIndicators?.Any() == true)
        {
            _featuresUsed.Add(SkuIds.IS_001);
        }
    }

    public void ClientUsed(string clientId) => _clientsUsed.Add(clientId);

    public void IssuerUsed(string issuer) => _issuersUsed.Add(issuer);

    public LicenseUsageSummary GetSummary()
    {
        var entitledSkus = license.IsConfigured
            ? license.Entitlements.Select(e => Skus.Get(e.SkuId)?.Name).OfType<string>().ToList().AsReadOnly()
            : (IReadOnlyCollection<string>)[];
        return new LicenseUsageSummary(entitledSkus, _clientsUsed.Values, _issuersUsed.Values, _featuresUsed.Values);
    }

    private class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary = new();

        // We check if the dictionary contains the key first, because it
        // performs better given our workload. Typically, these sets will contain
        // a small number of elements, and won't change much over time (e.g.,
        // the first time we try to use DPoP, that gets added, and then all
        // subsequent requests with a proof don't need to do anything here).
        // ConcurrentDictionary's ContainsKey method is lock free, while TryAdd
        // always acquires a lock, so in the (by far more common) steady state,
        // the ContainsKey check is much faster.
        public bool Add(T item) => _dictionary.ContainsKey(item) ? false : _dictionary.TryAdd(item, 0);

        public IReadOnlyCollection<T> Values => _dictionary.Keys.ToList().AsReadOnly();
    }
}
