// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.v2;

internal class FeatureManager : IFeatureManager
{
    public FeatureManager(
        ILicenseAccessor license,
        ILogger<FeatureManager> logger)
    {
        _license = license;
        _logger = logger;
    }
    private readonly ILicenseAccessor _license;
    private readonly ILogger<FeatureManager> _logger;
    
    private ulong _usedFeatures = 0;

    public IEnumerable<LicenseFeature> UsedFeatures()
    {
        foreach (LicenseFeature feature in Enum.GetValues<LicenseFeature>())
        {
            if ((_usedFeatures & (ulong) feature) != 0)
            {
                yield return feature;
            }
        }
    }

    public void UseFeature(LicenseFeature feature)
    {
        if ( _license.Current.IsConfigured && 
            !_license.Current.IsEnabled(feature) && 
            !AlreadyWarned(feature))
        {
            lock (_lock)
            {
                // Two AlreadyWarned checks makes the hottest code path lock-free
                if (!AlreadyWarned(feature))
                {
                    _logger.LogWarning("Attempt to use feature {feature}, but the license does not allow it",
                        feature);
                    _warnedFeatures.Add(feature);
                }
            }
        }
        var featureMask = (ulong) feature;
        Interlocked.Or(ref _usedFeatures, featureMask);
    }

    private readonly object _lock = new();
    private readonly HashSet<LicenseFeature> _warnedFeatures = new();
    private bool AlreadyWarned(LicenseFeature feature) => _warnedFeatures.Contains(feature);
}
