// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Validates expiration of DPoP tokens and nonces.
/// </summary>
internal class DPoPExpirationValidator
{
    internal readonly TimeProvider TimeProvider;
    internal readonly ILogger<DPoPExpirationValidator> Logger;

    public DPoPExpirationValidator(TimeProvider timeProvider, ILogger<DPoPExpirationValidator> logger)
    {
        TimeProvider = timeProvider;
        Logger = logger;
    }

    /// <summary>
    /// Validates whether a time value has expired based on validity duration and clock skew.
    /// Returns true if the time is beyond the allowed limits, false otherwise.
    /// </summary>
    internal bool IsExpired(TimeSpan validityDuration, TimeSpan clockSkew, long time)
    {
        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var start = now + (int)clockSkew.TotalSeconds;
        if (start < time)
        {
            var diff = time - now;
            Logger.LogDebug("Expiration check failed. Creation time was too far in the future. The time being checked was {iat}, and clock is now {now}. The time difference is {diff}.", time, now, diff);
            return true;
        }

        var expiration = time + (int)validityDuration.TotalSeconds;
        var end = now - (int)clockSkew.TotalSeconds;
        if (expiration < end)
        {
            var diff = now - expiration;
            Logger.LogDebug("Expiration check failed. Expiration has already happened. The expiration was at {exp}, and clock is now {now}. The time difference is {diff}.", expiration, now, diff);
            return true;
        }

        return false;
    }
}
