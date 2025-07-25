// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal static class LicenseLogParameters
{
    public const string Threshold = "Threshold";
    public const string ClientLimit = "ClientLimit";
    public const string ClientCount = "ClientCount";
    public const string ClientsUsed = "ClientsUsed";
    public const string IssuerLimit = "IssuerLimit";
    public const string IssuerCount = "IssuerCount";
    public const string IssuersUsed = "IssuersUsed";
    public const string LicenseContact = "LicenseContact";
    public const string LicenseCompany = "LicenseCompany";
}

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Critical,
        message: "Error validating the Duende software license key")]
    public static partial void ErrorValidatingLicenseKey(this ILogger logger, Exception ex);

    [LoggerMessage(
        LogLevel.Error,
        message: $"Your IdentityServer license is expired. Please contact {{{LicenseLogParameters.LicenseContact}}} from {{{LicenseLogParameters.LicenseCompany}}} or start a conversation with us at https://duende.link/l/contact to renew your license as soon as possible. In a future version, license expiration will be enforced after a grace period. See https://duende.link/l/expired for more information.")]
    public static partial void LicenseHasExpired(this ILogger logger,
        string licenseContact, string licenseCompany);

    [LoggerMessage(
        LogLevel.Error,
        Message =
            $"You are using IdentityServer in trial mode and have exceeded the trial threshold of {{{LicenseLogParameters.Threshold}}} requests handled by IdentityServer. In a future version, you will need to restart the server or configure a license key to continue testing. See https://duende.link/l/trial for more information.")]
    public static partial void TrialModeRequestCountExceeded(this ILogger logger, ulong threshold);

    [LoggerMessage(
        LogLevel.Error,
        message:
            $"Your IdentityServer license includes {{{LicenseLogParameters.ClientLimit}}} clients but you have processed requests for {{{LicenseLogParameters.ClientCount}}} clients. Please contact {{{LicenseLogParameters.LicenseContact}}} from {{{LicenseLogParameters.LicenseCompany}}} or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The clients used were: {{{LicenseLogParameters.ClientsUsed}}}. See https://duende.link/l/threshold for more information.")]
    public static partial void ClientLimitExceededWithinOverageThreshold(this ILogger logger,
        int clientLimit, int clientCount, string licenseContact, string licenseCompany, IReadOnlyCollection<string> clientsUsed);

    // Language is deliberately the same when over or under threshold (will change in future version).
    [LoggerMessage(
        LogLevel.Error,
        message:
            $"Your IdentityServer license includes {{{LicenseLogParameters.ClientLimit}}} clients but you have processed requests for {{{LicenseLogParameters.ClientCount}}} clients. Please contact {{{LicenseLogParameters.LicenseContact}}} from {{{LicenseLogParameters.LicenseCompany}}} or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The clients used were: {{{LicenseLogParameters.ClientsUsed}}}. See https://duende.link/l/threshold for more information.")]
    public static partial void ClientLimitExceededOverThreshold(this ILogger logger,
        int clientLimit, int clientCount, string licenseContact, string licenseCompany, IReadOnlyCollection<string> clientsUsed);

    [LoggerMessage(
        LogLevel.Error,
        message:
        $"You are using IdentityServer in trial mode and have processed requests for {{{LicenseLogParameters.ClientCount}}} clients. In production, this will require a license with sufficient client capacity. You can either purchase a license tier that includes this many clients or add additional client capacity to a Starter Edition license. The clients used were: {{{LicenseLogParameters.ClientsUsed}}}. See https://duende.link/l/trial for more information.")]
    public static partial void ClientLimitWithNoLicenseExceeded(this ILogger logger, int clientCount,
        IReadOnlyCollection<string> clientsUsed);

    [LoggerMessage(
        LogLevel.Error,
        message: $"Your license for IdentityServer includes {{{LicenseLogParameters.IssuerLimit}}} issuer(s) but you have processed requests for {{{LicenseLogParameters.IssuerCount}}} issuers. This indicates that requests for each issuer are being sent to this instance of IdentityServer, which may be due to a network infrastructure configuration issue. If you intend to use multiple issuers, please contact {{{LicenseLogParameters.LicenseContact}}} from {{{LicenseLogParameters.LicenseCompany}}} or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The issuers used were {{{LicenseLogParameters.IssuersUsed}}}. See https://duende.link/l/threshold for more information.")]
    public static partial void IssuerLimitExceededWithinOverageThreshold(this ILogger logger,
        int issuerLimit, int issuerCount, string licenseContact, string licenseCompany, IReadOnlyCollection<string> issuersUsed);

    // Language is deliberately the same when over or under threshold (will change in future version).
    [LoggerMessage(
        LogLevel.Error,
        message: $"Your license for IdentityServer includes {{{LicenseLogParameters.IssuerLimit}}} issuer(s) but you have processed requests for {{{LicenseLogParameters.IssuerCount}}} issuers. This indicates that requests for each issuer are being sent to this instance of IdentityServer, which may be due to a network infrastructure configuration issue. If you intend to use multiple issuers, please contact {{{LicenseLogParameters.LicenseContact}}} from {{{LicenseLogParameters.LicenseCompany}}} or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The issuers used were {{{LicenseLogParameters.IssuersUsed}}}. See https://duende.link/l/threshold for more information.")]
    public static partial void IssuerLimitExceededOverThreshold(this ILogger logger,
        int issuerLimit, int issuerCount, string licenseContact, string licenseCompany, IReadOnlyCollection<string> issuersUsed);


    [LoggerMessage(
        LogLevel.Error,
        message: $"You are using IdentityServer in trial mode and have processed requests for {{{LicenseLogParameters.IssuerCount}}} issuers. This indicates that requests for each issuer are being sent to this instance of IdentityServer, which may be due to a network infrastructure configuration issue. If you intend to use multiple issuers, either a license per issuer or an Enterprise Edition license is required. In a future version, this limit will be enforced after a threshold is exceeded. The issuers used were: {{{LicenseLogParameters.IssuersUsed}}}. See https://duende.link/l/trial for more information.")]
    public static partial void IssuerLimitWithNoLicenseExceeded(this ILogger logger, int issuerCount, IReadOnlyCollection<string> issuersUsed);
}
