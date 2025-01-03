// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable disable

using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Duende;

// shared APIs needed for Duende license validation
internal partial class LicenseValidator
{
    static readonly string[] LicenseFileNames = new[]
    {
        "Duende_License.key",
        "Duende_IdentityServer_License.key",
    };

    static ILogger _logger;
    static Action<string, object[]> _errorLog;
    static Action<string, object[]> _informationLog;
    static Action<string, object[]> _warningLog;
    static Action<string, object[]> _debugLog;

    static License _license;

    static void Initalize(ILoggerFactory loggerFactory, string productName, string key, bool isDevelopment = false)
    {
        _logger = loggerFactory.CreateLogger($"Duende.{productName}.License");

        key ??= LoadFromFile();
        _license = ValidateKey(key);

        if (_license?.RedistributionFeature == true && !isDevelopment)
        {
            // for redistribution/prod scenarios, we want most of these to be at trace level
            _errorLog = _warningLog = _informationLog = _debugLog = LogToTrace;
        }
        else
        {
            _errorLog = LogToError;
            _warningLog = LogToWarning;
            _informationLog = LogToInformation;
            _debugLog = LogToDebug;
        }
    }

    private static string LoadFromFile()
    {
        foreach (var name in LicenseFileNames)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), name);
            if (File.Exists(path))
            {
                return File.ReadAllText(path).Trim();
            }
        }

        return null;
    }

    public static void ValidateLicense()
    {
        if (_logger == null) throw new Exception("LicenseValidator.Initalize has not yet been called.");

        var errors = new List<string>();

        if (_license == null)
        {
            var message = "You do not have a valid license key for the Duende software. " +
                          "This is allowed for development and testing scenarios. " +
                          "If you are running in production you are required to have a licensed version. " +
                          "Please start a conversation with us: https://duendesoftware.com/contact";

            // we're not using our _warningLog because we always want this emitted regardless of the context
            _logger.LogWarning(message);
            WarnForProductFeaturesWhenMissingLicense();
            return;
        }

        _debugLog.Invoke("The Duende license key details: {@license}", new[] { _license });

        if (_license.Expiration.HasValue)
        {
            var diff = DateTime.UtcNow.Date.Subtract(_license.Expiration.Value.Date).TotalDays;
            if (diff > 0 && !_license.RedistributionFeature)
            {
                errors.Add($"Your license for the Duende software expired {diff} days ago.");
            }
        }

        ValidateProductFeaturesForLicense(errors);

        if (errors.Count > 0)
        {
            foreach (var err in errors)
            {
                _errorLog.Invoke(err, Array.Empty<object>());
            }

            _errorLog.Invoke(
                "Please contact {licenseContact} from {licenseCompany} to obtain a valid license for the Duende software.",
                new[] { _license.ContactInfo, _license.CompanyName });
        }
        else
        {
            if (_license.Expiration.HasValue)
            {
                _informationLog.Invoke("You have a valid license key for the Duende software {edition} edition for use at {licenseCompany}. The license expires on {licenseExpiration}.",
                    new object[] { _license.Edition, _license.CompanyName, _license.Expiration.Value.ToLongDateString() });
            }
            else
            {
                _informationLog.Invoke(
                    "You have a valid license key for the Duende software {edition} edition for use at {licenseCompany}.",
                    new object[] { _license.Edition, _license.CompanyName });
            }
        }
    }

    internal static License ValidateKey(string licenseKey)
    {
        if (!String.IsNullOrWhiteSpace(licenseKey))
        {
            var handler = new JsonWebTokenHandler();

            var rsa = new RSAParameters
            {
                Exponent = Convert.FromBase64String("AQAB"),
                Modulus = Convert.FromBase64String(
                    "tAHAfvtmGBng322TqUXF/Aar7726jFELj73lywuCvpGsh3JTpImuoSYsJxy5GZCRF7ppIIbsJBmWwSiesYfxWxBsfnpOmAHU3OTMDt383mf0USdqq/F0yFxBL9IQuDdvhlPfFcTrWEL0U2JsAzUjt9AfsPHNQbiEkOXlIwtNkqMP2naynW8y4WbaGG1n2NohyN6nfNb42KoNSR83nlbBJSwcc3heE3muTt3ZvbpguanyfFXeoP6yyqatnymWp/C0aQBEI5kDahOU641aDiSagG7zX1WaF9+hwfWCbkMDKYxeSWUkQOUOdfUQ89CQS5wrLpcU0D0xf7/SrRdY2TRHvQ=="),
            };

            var key = new RsaSecurityKey(rsa)
            {
                KeyId = "IdentityServerLicensekey/7ceadbb78130469e8806891025414f16"
            };

            var parms = new TokenValidationParameters
            {
                ValidIssuer = "https://duendesoftware.com",
                ValidAudience = "IdentityServer",
                IssuerSigningKey = key,
                ValidateLifetime = false
            };
            var validateResult = handler.ValidateTokenAsync(licenseKey, parms).Result;
            if (validateResult.IsValid)
            {
                return new License(new ClaimsPrincipal(validateResult.ClaimsIdentity));
            }
            else
            {
                _logger.LogCritical(validateResult.Exception, "Error validating the Duende software license key");
            }
        }

        return null;
    }

    private static void LogToTrace(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            LoggerExtensions.LogTrace(_logger, message, args);
        }
    }

    private static void LogToDebug(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LoggerExtensions.LogDebug(_logger, message, args);
        }
    }

    private static void LogToInformation(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LoggerExtensions.LogInformation(_logger, message, args);
        }
    }

    private static void LogToWarning(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            LoggerExtensions.LogWarning(_logger, message, args);
        }
    }

    private static void LogToError(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            LoggerExtensions.LogError(_logger, message, args);
        }
    }
}