// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.v2;

internal class ProtocolRequestCounter : IProtocolRequestCounter
{
    public ProtocolRequestCounter(
        ILicenseAccessor license,
        TimeProvider timeProvider,
        ILogger<ProtocolRequestCounter> logger)
    {
        _license = license;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// The number of protocol requests allowed for unlicensed use. This should only be changed in tests.
    /// </summary>
    internal uint Threshold = 1000;

    private bool _requestsWarned;
    private bool _expiredLicenseWarned;

    private readonly ILicenseAccessor _license;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProtocolRequestCounter> _logger;

    private TimeSpan _gracePeriod = TimeSpan.FromDays(90);
    
    private bool IsExpired => _timeProvider.GetUtcNow() > _license.Current.Expiration;

    private uint _requestCount;
    public uint RequestCount => _requestCount;

    public void Increment()
    {
        if (!_license.Current.IsConfigured)
        {
            var total = Interlocked.Increment(ref _requestCount);

            if (total > Threshold && !_requestsWarned)
            {
                _logger.LogWarning("IdentityServer has handled {total} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license", total, Threshold);
                _requestsWarned = true;
            }
        }
        else if (!_expiredLicenseWarned && !_license.Current.Redistribution && IsExpired) 
        {
            _expiredLicenseWarned = true;
            _logger.LogWarning(
                "Your license expired on {expirationDate}. You are required to obtain a new license. In a future version of IdentityServer, expired licenses will stop the server after {gracePeriod} days.",
                _license.Current.Expiration?.ToString("yyyy-MM-dd"), _gracePeriod.Days);
        }
    }
}