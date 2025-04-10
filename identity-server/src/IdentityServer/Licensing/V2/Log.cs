// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal static class LicenseLogParameters
{
    public const string Threshold = "Threshold";
}

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Critical,
        message: "Error validating the Duende software license key")]
    public static partial void ErrorValidatingLicenseKey(this ILogger logger, Exception ex);

    [LoggerMessage(
        LogLevel.Error,
        message: "The IdentityServer license is expired. In a future version of IdentityServer, license expiration will be enforced after a grace period.")]
    public static partial void LicenseHasExpired(this ILogger logger);

    [LoggerMessage(LogLevel.Error,
        Message =
            $"You are using IdentityServer in trial mode and have exceeded the trial threshold of {{{LicenseLogParameters.Threshold}}} requests handled by IdentityServer. In a future version, you will need to restart the server or configure a license key to continue testing. For more information, please see https://docs.duendesoftware.com/trial-mode.")]
    public static partial void TrialModeRequestCountExceeded(this ILogger logger, ulong threshold);
}
