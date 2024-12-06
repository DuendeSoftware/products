// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Threading;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.v2;

internal class ProtocolRequestCounter : IProtocolRequestCounter
{
    public ProtocolRequestCounter(
        ILicenseAccessor license,
        ILoggerFactory loggerFactory)
    {
        _license = license;
        _logger = loggerFactory.CreateLogger("Duende.IdentityServer.License");
    }

    /// <summary>
    /// The number of protocol requests allowed for unlicensed use. This should only be changed in tests.
    /// </summary>
    internal uint Threshold = 500;

    private readonly ILicenseAccessor _license;
    private readonly ILogger _logger;

    private bool _warned;

    private uint _requestCount;
    public uint RequestCount => _requestCount;
    public void Increment()
    {
        if (!_license.Current.IsConfigured)
        {
            var total = Interlocked.Increment(ref _requestCount);

            if (total > Threshold && !_warned)
            {
                _logger.LogError("IdentityServer has handled {total} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license", total, Threshold);
                _warned = true;
            }
        }
    }
}
