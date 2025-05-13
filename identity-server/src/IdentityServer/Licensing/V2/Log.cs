// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal static class LicenseLogParameters
{
    public const string Threshold = "Threshold";
    public const string ClientLimit = "ClientLimit";
    public const string ClientCount = "ClientCount";
    public const string ClientLimitExceededThreshold = "ClientLimitExceededThreshold";
    public const string ClientsUsed = "ClientsUsed";
    public const string IssuerLimit = "IssuerLimit";
    public const string IssuerCount = "IssuerCount";
    public const string IssuerLimitExceededThreshold = "IssuerLimitExceededThreshold";
    public const string IssuersUsed = "IssuersUsed";
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

    [LoggerMessage(
        LogLevel.Error,
        Message =
            $"You are using IdentityServer in trial mode and have exceeded the trial threshold of {{{LicenseLogParameters.Threshold}}} requests handled by IdentityServer. In a future version, you will need to restart the server or configure a license key to continue testing. For more information, please see http://duende.link/trialmode.")]
    public static partial void TrialModeRequestCountExceeded(this ILogger logger, ulong threshold);

    [LoggerMessage(
        LogLevel.Error,
        message: $"Your license for Duende IdentityServer only permits {{{LicenseLogParameters.ClientLimit}}} number of clients. You have processed requests for {{{LicenseLogParameters.ClientCount}}} clients and are still within the threshold of {{{LicenseLogParameters.ClientLimitExceededThreshold}}} for exceeding permitted clients. In a future version of client limit will be enforced. The clients used were: {{{LicenseLogParameters.ClientsUsed}}}.")]
    public static partial void ClientLimitExceededWithinOverageThreshold(this ILogger logger, int clientLimit,
        int clientCount, int clientLimitExceededThreshold, IReadOnlyCollection<string> clientsUsed);

    [LoggerMessage(
        LogLevel.Error,
        message:
            $"Your license for Duende IdentityServer only permits {{{LicenseLogParameters.ClientLimit}}} number of clients. You have processed requests for {{{LicenseLogParameters.ClientCount}}} clients and are beyond the threshold of {{{LicenseLogParameters.ClientLimitExceededThreshold}}} for exceeding permitted clients. In a future version of client limit will be enforced. The clients used were: {{{LicenseLogParameters.ClientsUsed}}}.")]
    public static partial void ClientLimitExceededOverThreshold(this ILogger logger, int clientLimit, int clientCount,
        int clientLimitExceededThreshold, IReadOnlyCollection<string> clientsUsed);

    [LoggerMessage(
        LogLevel.Error,
        message:
        $"You do not have a license, and you have processed requests for {{{LicenseLogParameters.ClientCount}}} clients. This number requires a tier of license higher than Starter Edition. The clients used were: {{{LicenseLogParameters.ClientsUsed}}}.")]
    public static partial void ClientLimitWithNoLicenseExceeded(this ILogger logger, int clientCount,
        IReadOnlyCollection<string> clientsUsed);

    [LoggerMessage(
        LogLevel.Error,
        message: $"Your license for Duende IdentityServer only permits {{{LicenseLogParameters.IssuerLimit}}} number of issuers. You have processed requests for {{{LicenseLogParameters.IssuerCount}}} issuers and are still within the threshold of {{{LicenseLogParameters.IssuerLimitExceededThreshold}}}. The issuers used were: {{{LicenseLogParameters.IssuersUsed}}}. This might be due to your server being accessed via different URLs or a direct IP and/or you have reverse proxy or a gateway involved. This suggests a network infrastructure configuration problem, or you are deliberately hosting multiple URLs and require an upgraded license. In a future version of issuer limit will be enforced.")]
    public static partial void IssuerLimitExceededWithinOverageThreshold(this ILogger logger, int issuerLimit, int issuerCount, int issuerLimitExceededThreshold, IReadOnlyCollection<string> issuersUsed);

    [LoggerMessage(
        LogLevel.Error,
        message: $"Your license for Duende IdentityServer only permits {{{LicenseLogParameters.IssuerLimit}}} number of issuers. You have processed requests for {{{LicenseLogParameters.IssuerCount}}} issuers and are over the threshold of {{{LicenseLogParameters.IssuerLimitExceededThreshold}}}. The issuers used were: {{{LicenseLogParameters.IssuersUsed}}}. This might be due to your server being accessed via different URLs or a direct IP and/or you have reverse proxy or a gateway involved. This suggests a network infrastructure configuration problem, or you are deliberately hosting multiple URLs and require an upgraded license. In a future version of issuer limit will be enforced.")]
    public static partial void IssuerLimitExceededOverThreshold(this ILogger logger, int issuerLimit, int issuerCount, int issuerLimitExceededThreshold, IReadOnlyCollection<string> issuersUsed);

    [LoggerMessage(
        LogLevel.Error,
        message: $"You do not have a license, and you have processed requests for {{{LicenseLogParameters.IssuerCount}}} issuers. If you are deliberately hosting multiple URLs then this number requires a license per issuer, or the Enterprise Edition tier of license. If not then this might be due to your server being accessed via different URLs or a direct IP and/or you have reverse proxy or a gateway involved, and this suggests a network infrastructure configuration problem. The issuers used were: {{{LicenseLogParameters.IssuersUsed}}}.")]
    public static partial void IssuerLimitWithNoLicenseExceeded(this ILogger logger, int issuerCount, IReadOnlyCollection<string> issuersUsed);
}
