// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Threading;
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
        _logger.LogError("IdentityServer has handled {total} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license", total, Threshold);
        _warned = true;
    }
}
