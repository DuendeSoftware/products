// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Duende.IdentityServer.Licensing.V2;

internal class LicenseUsageTracker(LicenseAccessor licenseAccessor)
{
    private readonly ConcurrentHashSet<LicenseFeature> _otherFeatures = new();
    private readonly ConcurrentHashSet<LicenseFeature> _businessFeatures = new();
    private readonly ConcurrentHashSet<LicenseFeature> _enterpriseFeatures = new();
    private readonly ConcurrentHashSet<string> _clientsUsed = new();
    private readonly ConcurrentHashSet<string> _issuersUsed = new();

    public void FeatureUsed(LicenseFeature feature)
    {
        switch (feature)
        {
            case LicenseFeature.ResourceIsolation:
            case LicenseFeature.DynamicProviders:
            case LicenseFeature.CIBA:
            case LicenseFeature.DPoP:
                _enterpriseFeatures.Add(feature);
                break;
            case LicenseFeature.KeyManagement:
            case LicenseFeature.PAR:
            case LicenseFeature.ServerSideSessions:
            case LicenseFeature.DCR:
                _businessFeatures.Add(feature);
                break;
            case LicenseFeature.ISV:
            case LicenseFeature.Redistribution:
                _otherFeatures.Add(feature);
                break;
        }
    }

    public void ClientUsed(string clientId) => _clientsUsed.Add(clientId);

    public void IssuerUsed(string issuer) => _issuersUsed.Add(issuer);

    public LicenseUsageSummary GetSummary()
    {
        var licenseEdition = licenseAccessor.Current.Edition?.ToString() ?? "None";
        var featuresUsed = _enterpriseFeatures.Values
            .Concat(_businessFeatures.Values)
            .Concat(_otherFeatures.Values)
            .Select(f => f.ToString())
            .ToList()
            .AsReadOnly();
        return new LicenseUsageSummary(licenseEdition, _clientsUsed.Values, _issuersUsed.Values, featuresUsed);
    }

    private class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary = new();

        public bool Add(T item) => _dictionary.TryAdd(item, 0);

        public IReadOnlyCollection<T> Values => _dictionary.Keys.ToList().AsReadOnly();
    }
}