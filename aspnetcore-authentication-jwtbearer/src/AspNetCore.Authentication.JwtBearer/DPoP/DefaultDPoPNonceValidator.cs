// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Default implementation of <see cref="IDPoPNonceValidator"/> that creates and
/// validates nonces using data protected server time stamps.
/// </summary>
internal class DefaultDPoPNonceValidator : IDPoPNonceValidator
{
    private const string DataProtectorPurpose = "DPoPProofValidator-nonce";

    internal readonly IOptionsMonitor<DPoPOptions> OptionsMonitor;
    internal readonly IDataProtector DataProtector;
    internal readonly TimeProvider TimeProvider;
    internal readonly ILogger<DefaultDPoPNonceValidator> Logger;
    internal readonly DPoPExpirationValidator ExpirationValidator;

    public DefaultDPoPNonceValidator(
        IOptionsMonitor<DPoPOptions> optionsMonitor,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider,
        ILogger<DefaultDPoPNonceValidator> logger,
        DPoPExpirationValidator expirationValidator)
    {
        OptionsMonitor = optionsMonitor;
        DataProtector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
        TimeProvider = timeProvider;
        Logger = logger;
        ExpirationValidator = expirationValidator;
    }

    public string CreateNonce(DPoPProofValidationContext context)
    {
        var now = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        return DataProtector.Protect(now.ToString());
    }

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

    internal bool IsExpired(DPoPProofValidationContext context, long time)
    {
        var dpopOptions = OptionsMonitor.Get(context.Scheme);
        var validityDuration = dpopOptions.ProofTokenValidityDuration;
        var skew = dpopOptions.ServerClockSkew;

        return ExpirationValidator.IsExpired(validityDuration, skew, time);
    }
}
