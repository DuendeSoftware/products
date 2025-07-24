// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal class LicenseUsageTracker(LicenseAccessor licenseAccessor, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Duende.IdentityServer.License");

    private readonly ConcurrentHashSet<LicenseFeature> _otherFeatures = new();
    private readonly ConcurrentHashSet<LicenseFeature> _businessFeatures = new();
    private readonly ConcurrentHashSet<LicenseFeature> _enterpriseFeatures = new();
    private readonly ConcurrentHashSet<string> _clientsUsed = new();
    private readonly ConcurrentHashSet<string> _issuersUsed = new();

    private const int ClientLimitExceededThreshold = 5;
    private const int IssuerLimitExceededThreshold = 1;

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

    public void ClientUsed(string clientId)
    {
        var initialClientCount = _clientsUsed.Values.Count;

        _clientsUsed.Add(clientId);

        if (initialClientCount == _clientsUsed.Values.Count)
        {
            return;
        }

        var license = licenseAccessor.Current;

        if (license.IsConfigured)
        {
            if (license.Redistribution || !license.ClientLimit.HasValue)
            {
                return;
            }

            var clientLimitOverage = _clientsUsed.Values.Count - license.ClientLimit;
            switch (clientLimitOverage)
            {
                case > ClientLimitExceededThreshold:
                    _logger.ClientLimitExceededOverThreshold(license.ClientLimit.Value, _clientsUsed.Values.Count, license.ContactInfo, license.CompanyName, _clientsUsed.Values);
                    break;
                case > 0:
                    _logger.ClientLimitExceededWithinOverageThreshold(license.ClientLimit.Value, _clientsUsed.Values.Count, license.ContactInfo, license.CompanyName, _clientsUsed.Values);
                    break;
            }
        }
        else
        {
            if (_clientsUsed.Values.Count > ClientLimitExceededThreshold)
            {
                _logger.ClientLimitWithNoLicenseExceeded(_clientsUsed.Values.Count, _clientsUsed.Values);
            }
        }
    }

    public void IssuerUsed(string issuer)
    {
        var initialIssuerCount = _issuersUsed.Values.Count;

        _issuersUsed.Add(issuer);

        if (initialIssuerCount == _issuersUsed.Values.Count)
        {
            return;
        }

        var license = licenseAccessor.Current;

        if (license.IsConfigured)
        {
            if (license.Redistribution || !license.IssuerLimit.HasValue)
            {
                return;
            }

            var issuerLimitOverage = _issuersUsed.Values.Count - license.IssuerLimit;
            switch (issuerLimitOverage)
            {
                case > IssuerLimitExceededThreshold:
                    _logger.IssuerLimitExceededOverThreshold(license.IssuerLimit.Value, _issuersUsed.Values.Count, license.ContactInfo, license.CompanyName, _issuersUsed.Values);
                    break;
                case > 0:
                    _logger.IssuerLimitExceededWithinOverageThreshold(license.IssuerLimit.Value, _issuersUsed.Values.Count, license.ContactInfo, license.CompanyName, _issuersUsed.Values);
                    break;
            }
        }
        else
        {
            if (_issuersUsed.Values.Count > IssuerLimitExceededThreshold)
            {
                _logger.IssuerLimitWithNoLicenseExceeded(_issuersUsed.Values.Count, _issuersUsed.Values);
            }
        }
    }

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
