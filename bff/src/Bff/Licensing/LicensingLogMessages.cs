// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff.Licensing;

internal static partial class LicensingLogMessages
{
    [LoggerMessage(
        Message = """
                    Duende BFF security Framework License information:
                     - Edition: {Edition}
                     - Expiration: {ExpirationDate}
                     - LicenseContact: {LicenseContact}
                     - LicenseContact: {LicenseContact}
                     - Number of frontends licensed: {NumberOfFrontends}
                    """)]
    public static partial void LicenseDetails(this ILogger logger, LogLevel level, string? edition, DateTimeOffset? expirationDate, string licenseContact, string licenseCompany, string? numberOfFrontends);

    [LoggerMessage(
        Message = """
                    Your license for Duende BFF security framework has expired on {ExpirationDate}. 
                    Please contact {licenseContact} from {licenseCompany} to obtain a valid license for the Duende software,
                    or start a conversation with us: https://duendesoftware.com/contact.
                    """)]
    public static partial void LicenseHasExpired(this ILogger logger, LogLevel level, DateTimeOffset? expirationDate, string licenseContact, string licenseCompany);


    [LoggerMessage(
        message: """
                   You do not have a valid license key for the Duende BFF security framework.
                   This is allowed for development and testing scenarios.
                   If you are running in production you are required to have a licensed version.
                   Please start a conversation with us: https://duendesoftware.com/contact
                   """)]
    public static partial void NoValidLicense(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: """
                   Your license key does not include the BFF feature.
                   Please contact {LicenseContact} from {LicenseCompany} to obtain a valid license for the Duende software,
                   or start a conversation with us: https://duendesoftware.com/contact.
                   """)]
    public static partial void NotLicensedForBff(this ILogger logger, LogLevel logLevel, string licenseContact, string licenseCompany);

    [LoggerMessage(
        message: "Error validating the license key" +
                 "If you are running in production you are required to have a licensed version. " +
                 "Please start a conversation with us: https://duendesoftware.com/contact")]
    public static partial void ErrorValidatingLicenseKey(this ILogger logger, LogLevel logLevel, Exception ex);
}
