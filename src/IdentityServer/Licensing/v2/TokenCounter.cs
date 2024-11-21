// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.v2;

internal class TokenCounter : ITokenCounter
{
    public TokenCounter(
        ILicenseAccessor license,
        TimeProvider timeProvider,
        ILogger<TokenCounter> logger)
    {
        _license = license;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// The number of tokens allowed for unlicensed use. This should only be changed in tests.
    /// </summary>
    internal uint Threshold = 1000;

    private bool _issuedTokensWarned;
    private bool _expiredLicenseWarned;

    private readonly ILicenseAccessor _license;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokenCounter> _logger;

    private TimeSpan _gracePeriod = TimeSpan.FromDays(90);
    
    private bool IsExpired => _timeProvider.GetUtcNow() > _license.Current.Expiration;

    private uint _tokenCount;
    public uint TokenCount => _tokenCount;

    public void Increment()
    {
        if (!_license.Current.IsConfigured)
        {
            var total = Interlocked.Increment(ref _tokenCount);

            if (total > Threshold && !_issuedTokensWarned)
            {
                _logger.LogWarning("You've issued {total} tokens without a license. In future versions, unlicensed IdentityServer instances will shut down after {threshold} tokens are issued. Please contact sales to obtain a license. If you are running in a test environment, please use a test license", total, Threshold);
                _issuedTokensWarned = true;
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