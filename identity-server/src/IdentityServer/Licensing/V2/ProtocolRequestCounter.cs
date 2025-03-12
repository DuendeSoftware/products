// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal class ProtocolRequestCounter(
    LicenseAccessor license,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Duende.IdentityServer.License");
    private bool _warned;
    private ulong _requestCount;

    /// <summary>
    /// The number of protocol requests allowed for unlicensed use. This should only be changed in tests.
    /// </summary>
    internal ulong Threshold = 500;

    internal ulong RequestCount => _requestCount;

    internal void Increment()
    {
        if (license.Current.IsConfigured)
        {
            return;
        }
        var total = Interlocked.Increment(ref _requestCount);
        if (total <= Threshold || _warned)
        {
            return;
        }
        _logger.LogError($"You are using IdentityServer in trial mode and have exceeded the trial threshold of {Threshold} requests handled by IdentityServer. In a future version, you will need to restart the server or configure a license key to continue testing. For more information, please see https://docs.duendesoftware.com/trial-mode.");
        _warned = true;
    }
}
