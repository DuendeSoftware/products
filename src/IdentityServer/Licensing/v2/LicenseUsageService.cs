// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Duende.IdentityServer.Licensing.v2;

internal class LicenseUsageService : ILicenseUsageService
{
    private void EnsureAdded<T>(ref HashSet<T> hashSet, object lockObject, T key)
    {
        // Lock free test first.
        if (!hashSet.Contains(key))
        {
            lock (lockObject)
            {
                // Check again after lock, to quit early if another thread
                // already did the job.
                if (!hashSet.Contains(key))
                {
                    // The HashSet is not thread safe. And we don't want to lock for every single
                    // time we use it. Our access pattern should be a lot of reads and a few writes
                    // so better to create a new copy every time we need to add a value.
                    var newSet = new HashSet<T>(hashSet)
                    {
                        key
                    };

                    // Reference assignment is atomic so non-locked readers will handle this.
                    hashSet = newSet;
                }
            }
        }
    }

    // Features
    private readonly object _featureLock = new();
    private HashSet<LicenseFeature> _features = new();
    public HashSet<LicenseFeature> UsedFeatures => _features;
    public void UseFeature(LicenseFeature feature) => EnsureAdded(ref _features, _featureLock, feature);

    // Clients
    private readonly object _clientLock = new();
    private HashSet<string> _clients = new();
    public HashSet<string> UsedClients => _clients;
    public void UseClient(string clientId) => EnsureAdded(ref _clients, _clientLock, clientId);

    // Issuers
    private readonly object _issuerLock = new();
    private HashSet<string> _issuers = new();
    public HashSet<string> UsedIssuers => _issuers;
    public void UseIssuer(string issuer) => EnsureAdded(ref _issuers, _issuerLock, issuer);
}
