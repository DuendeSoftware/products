// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// using Microsoft.Extensions.Logging;

// namespace Duende.Bff.Licensing;

// internal static partial class LicensingLogMessages
// {
//     [LoggerMessage(
//         Message = """
//                     Duende BFF Security Framework License information:
//                      - Edition: {Edition}
//                      - Expiration: {ExpirationDate}
//                      - LicenseContact: {LicenseContact}
//                      - LicenseCompany: {licenseCompany}
//                      - Number of frontends licensed: {NumberOfFrontends}
//                     """)]
//     public static partial void LicenseDetails(this ILogger logger, LogLevel level, string? edition, DateTimeOffset? expirationDate, string licenseContact, string licenseCompany, string? numberOfFrontends);
//
//     [LoggerMessage(
//         Message = """
//                     Your license for Duende BFF Security Framework has expired on {ExpirationDate}.
//                     Please contact {licenseContact} from {licenseCompany} to obtain a valid license for the Duende software,
//                     or start a conversation with us: https://duende.link/l/bff/contact
//
//                     See https://duende.link/l/bff/expired for more information.
//                     """)]
//     public static partial void LicenseHasExpired(this ILogger logger, LogLevel level, DateTimeOffset? expirationDate, string licenseContact, string licenseCompany);
//
//
//     [LoggerMessage(
//         message: """
//                    You do not have a valid license key for the Duende BFF Security Framework.
//                    When unlicensed, BFF will run in trial mode. It will limit the number of active sessions to 5.
//                    If you are running in production you are required to have a licensed version.
//                    Please start a conversation with us: https://duende.link/l/bff/contact
//
//                    See https://duende.link/l/bff/trial for more information.
//                    """)]
//     public static partial void NoValidLicense(this ILogger logger, LogLevel logLevel);
//
//     [LoggerMessage(
//         message: """
//                    Your license key does not include the BFF feature.
//                    BFF will run in trial mode. It will limit the number of active sessions to 5.
//                    Please contact {LicenseContact} from {LicenseCompany} to obtain a valid license for the Duende software,
//                    or start a conversation with us: https://duende.link/l/bff/contact
//
//                    See https://duende.link/l/bff/trial for more information.
//                    """)]
//     public static partial void NotLicensedForBff(this ILogger logger, LogLevel logLevel, string licenseContact, string licenseCompany);
//
//     [LoggerMessage(
//         message: "Error validating the license key." +
//                  "If you are running in production you are required to have a licensed version. " +
//                  "Please start a conversation with us: https://duende.link/l/bff/contact")]
//     public static partial void ErrorValidatingLicenseKey(this ILogger logger, LogLevel logLevel, Exception ex);
//
//     [LoggerMessage(
//         message: """
//                  Frontend #{FrontendsUsed} with name {FrontendName} was added. The license allows for unlimited frontends.
//                  """)]
//     public static partial void UnlimitedFrontends(this ILogger logger, LogLevel logLevel, string frontendName,
//         int frontendsUsed);
//     [LoggerMessage(
//         message: """
//                  Frontend {FrontendName} was added. Currently using {frontendsUsed} of {frontendLimit} in the BFF License.
//                  """)]
//     public static partial void FrontendAdded(this ILogger logger, LogLevel logLevel, string frontendName,
//         int frontendsUsed, int frontendLimit);
//
//     [LoggerMessage(
//         message: """
//                  Frontend {FrontendName} was added. This exceeds the maximum number of frontends allowed by your license.
//                  Currently using {frontendsUsed} of {frontendLimit} in the BFF License.
//
//                  See https://duende.link/l/bff/threshold for more information.
//                  """)]
//     public static partial void FrontendLimitExceeded(this ILogger logger, LogLevel logLevel, string frontendName,
//         int frontendsUsed, int frontendLimit);
//
//     [LoggerMessage(
//         message: """
//                  Frontend {FrontendName} was added. However, your current license does not support multiple frontends.
//                  If you are running in production you are required to have a license for each frontend.
//                  Please start a conversation with us: https://duende.link/l/bff/contact
//
//                  See https://duende.link/l/bff/threshold for more information.
//                  """)]
//     public static partial void NotLicensedForMultiFrontend(this ILogger logger, LogLevel logLevel, string frontendName);
// }
