// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Default implementation of <see cref="IDPoPNonceValidator"/> that creates and validates nonces using data protection.
/// </summary>
internal class DefaultDPoPNonceValidator : IDPoPNonceValidator
{
    private const string DataProtectorPurpose = "DPoPProofValidator-nonce";

    /// <summary>
    /// Provides the options for DPoP proof validation.
    /// </summary>
    internal readonly IOptionsMonitor<DPoPOptions> OptionsMonitor;

    /// <summary>
    /// Protects and unprotects nonce values.
    /// </summary>
    internal readonly IDataProtector DataProtector;

    /// <summary>
    /// Clock for checking proof expiration.
    /// </summary>
    internal readonly TimeProvider TimeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    internal readonly ILogger<DefaultDPoPNonceValidator> Logger;

    /// <summary>
    /// Constructs a new instance of the <see cref="DefaultDPoPNonceValidator"/>.
    /// </summary>
    public DefaultDPoPNonceValidator(
        IOptionsMonitor<DPoPOptions> optionsMonitor,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider,
        ILogger<DefaultDPoPNonceValidator> logger)
    {
        OptionsMonitor = optionsMonitor;
        DataProtector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
        TimeProvider = timeProvider;
        Logger = logger;
    }

    /// <summary>
    /// Creates a nonce value to return to the client.
    /// </summary>
    public string CreateNonce(DPoPProofValidationContext context)
    {
        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        return DataProtector.Protect(now.ToString());
    }

    /// <summary>
    /// Validates the freshness of the nonce value.
    /// </summary>
    public NonceValidationResult ValidateNonce(DPoPProofValidationContext context, string? nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            return NonceValidationResult.Missing;
        }

        var time = GetUnixTimeFromNonce(nonce);
        if (time <= 0)
        {
            Logger.LogDebug("Invalid time value read from the 'nonce' value");
            return NonceValidationResult.Invalid;
        }

        if (IsExpired(context, time))
        {
            Logger.LogDebug("DPoP 'nonce' expired. Issuing new value to client.");
            return NonceValidationResult.Invalid;
        }

        return NonceValidationResult.Valid;
    }

    /// <summary>
    /// Reads the time the nonce was created.
    /// </summary>
    internal long GetUnixTimeFromNonce(string nonce)
    {
        try
        {
            var value = DataProtector.Unprotect(nonce);
            if (long.TryParse(value, out var iat))
            {
                return iat;
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Error parsing DPoP 'nonce' value: {error}", ex.ToString());
        }

        // We return 0 to indicate failure.
        return 0;
    }

    /// <summary>
    /// Validates the expiration of the DPoP proof nonce.
    /// Returns true if the time is beyond the allowed limits, false otherwise.
    /// </summary>
    internal bool IsExpired(DPoPProofValidationContext context, long time)
    {
        var dpopOptions = OptionsMonitor.Get(context.Scheme);
        var validityDuration = dpopOptions.ProofTokenValidityDuration;
        var skew = dpopOptions.ServerClockSkew;

        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var start = now + (int)skew.TotalSeconds;
        if (start < time)
        {
            var diff = time - now;
            Logger.LogDebug("Expiration check failed. Creation time was too far in the future. The time being checked was {iat}, and clock is now {now}. The time difference is {diff}", time, now, diff);
            return true;
        }

        var expiration = time + (int)validityDuration.TotalSeconds;
        var end = now - (int)skew.TotalSeconds;
        if (expiration < end)
        {
            var diff = now - expiration;
            Logger.LogDebug("Expiration check failed. Expiration has already happened. The expiration was at {exp}, and clock is now {now}. The time difference is {diff}", expiration, now, diff);
            return true;
        }

        return false;
    }
}
