// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.Private.Licencing.V2;
using Microsoft.Extensions.Logging;

namespace Duende.Licensing.Enforcement;

/// <summary>
/// Validates license entitlements for Duende product features.
/// Logs warnings or errors based on the gold rules - never blocks execution.
/// Rate-limits log output per SKU to avoid spamming the customer.
/// </summary>
internal sealed class LicenseValidator(
    V2License license,
    ILogger<LicenseValidator> logger,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastLogged = new();
    private readonly TimeSpan _logFrequency = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validates that a boolean (on/off) feature SKU is entitled.
    /// Logs a warning if no license is configured or if the SKU is missing.
    /// </summary>
    internal void ValidateFeature(string skuId, string featureName)
    {
        if (!license.IsConfigured)
        {
            if (ShouldLog(skuId))
            {
                logger.FeatureUsedNoLicense(featureName);
            }

            return;
        }

        if (!license.HasSku(skuId))
        {
            if (ShouldLog(skuId))
            {
                logger.FeatureNotLicensed(featureName);
            }
        }
    }

    /// <summary>
    /// Validates a quantized (count-based) SKU entitlement.
    /// Logs a warning if within grace, error if exceeded.
    /// </summary>
    internal void ValidateQuantized(string skuId, string featureName, int actual)
    {
        if (!license.IsConfigured)
        {
            if (ShouldLog(skuId))
            {
                logger.FeatureUsedNoLicense(featureName);
            }

            return;
        }

        var entitlement = license.GetEntitlement(skuId);
        if (entitlement is not { Limit: not null, Grace: not null })
        {
            return;
        }

        var status = GraceCalculator.Evaluate(actual, entitlement.Limit.Value, entitlement.Grace.Value);

        switch (status)
        {
            case UsageStatus.Warning:
                if (ShouldLog(skuId))
                {
                    logger.QuantizedExceedsLimit(featureName, actual, entitlement.Limit.Value, entitlement.Grace.Value);
                }

                break;
            case UsageStatus.Exceeded:
                if (ShouldLog(skuId))
                {
                    logger.QuantizedExceedsGrace(featureName, actual, entitlement.Grace.Value);
                }

                break;
        }
    }

    private bool ShouldLog(string skuId)
    {
        var now = timeProvider.GetUtcNow();

        if (_lastLogged.TryGetValue(skuId, out var lastTime) && now - lastTime < _logFrequency)
        {
            return false;
        }

        _lastLogged[skuId] = now;
        return true;
    }
}
