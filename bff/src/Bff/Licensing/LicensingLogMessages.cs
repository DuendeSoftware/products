// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// using Microsoft.Extensions.Logging;
//
// namespace Duende.Bff.Licensing;
//
// internal static partial class LicensingLogMessages
// {
//     [LoggerMessage(
//         Message = """
//                   Duende Software License information:
//                    - Expiration: {ExpirationDate}
//                    - LicenseContact: {LicenseContact}
//                    - LicenseCompany: {licenseCompany}
//                   """)]
//     public static partial void LicenseDetails(this ILogger logger, LogLevel level,
//         DateTimeOffset? expirationDate, string licenseContact, string licenseCompany);
//
//     [LoggerMessage(
//         Message = """
//                   Your license for the Duende software has expired on {ExpirationDate}.
//                   Please contact {licenseContact} from {licenseCompany} to obtain a valid license for Duende software,
//                   or start a conversation with us: https://duende.link/l/bff/contact
//
//                   See https://duende.link/l/bff/expired for more information.
//                   """)]
//     public static partial void LicenseHasExpired(this ILogger logger, LogLevel level, DateTimeOffset? expirationDate,
//         string licenseContact, string licenseCompany);
//
//
//     [LoggerMessage(
//         Message = """
//                   You do not have a valid license key for the Duende software.
//                   BFF will run in trial mode. This is allowed for development and testing scenarios.
//
//                   If you are running in production you are required to have a licensed version.
//                   Please start a conversation with us: https://duende.link/l/bff/contact"
//                   """)]
//     public static partial void NoValidLicense(this ILogger logger, LogLevel logLevel);
//
//     [LoggerMessage(
//         Message = """
//                   BFF is running in trial mode. The maximum number of allowed authenticated sessions ({MaximumAllowedSessionsInTrialMode}) has been exceeded.
//
//                   See https://duende.link/l/bff/trial for more information.
//                   """)]
//     public static partial void TrialModeWarning(this ILogger logger, LogLevel logLevel,
//         int maximumAllowedSessionsInTrialMode);
//
//     [LoggerMessage(
//         Message = "Error validating the license key." +
//                   "If you are running in production you are required to have a licensed version. " +
//                   "Please start a conversation with us: https://duende.link/l/bff/contact")]
//     public static partial void ErrorValidatingLicenseKey(this ILogger logger, LogLevel logLevel, Exception ex);
// }
