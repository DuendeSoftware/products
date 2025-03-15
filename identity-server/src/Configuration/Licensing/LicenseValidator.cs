// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable disable

using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende;

// shared APIs needed for Duende license validation
internal class LicenseValidator<T>
    where T : License, new()
{
    private static readonly string[] LicenseFileNames = new[]
    {
        "Duende_License.key",
        "Duende_IdentityServer_License.key",
    };

    protected ILogger Logger;
    protected Action<string, object[]> ErrorLog;
    protected Action<string, object[]> InformationLog;
    protected Action<string, object[]> WarningLog;
    protected Action<string, object[]> DebugLog;

    protected T License { get; private set; }

    // cloned copy meant to be accessible in DI
    private T _copy;
    public T GetLicense()
    {
        if (_copy == null && License != null)
        {
            _copy = new T();
            _copy.Initialize(License.Claims.Clone());
        }
        return _copy;
    }

    protected void Initialize(ILoggerFactory loggerFactory, string productName, string key)
    {
        Logger = loggerFactory.CreateLogger($"Duende.{productName}.License");

        key ??= LoadFromFile();
        License = ValidateKey(key);

        ErrorLog = LogToError;
        WarningLog = LogToWarning;
        InformationLog = LogToInformation;
        DebugLog = LogToDebug;
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

    protected void ValidateLicense()
    {
        if (License == null)
        {
            var message = "You do not have a valid license key for the Duende software. " +
                          "This is allowed for development and testing scenarios. " +
                          "If you are running in production you are required to have a licensed version. " +
                          "Please start a conversation with us: https://duendesoftware.com/contact";

            // we're not using our _warningLog because we always want this emitted regardless of the context
            Logger.LogWarning(message);
            WarnForProductFeaturesWhenMissingLicense();
            return;
        }

        DebugLog.Invoke("The Duende license key details: {@license}", new object[] { License });

        var errors = new List<string>();

        ValidateExpiration(errors);
        ValidateProductFeatures(errors);

        if (errors.Count > 0)
        {
            foreach (var err in errors)
            {
                ErrorLog.Invoke(err, Array.Empty<object>());
            }

            ErrorLog.Invoke(
                "Please contact {licenseContact} from {licenseCompany} to obtain a valid license for the Duende software.",
                new object[] { License.ContactInfo, License.CompanyName });
        }
        else
        {
            if (License.Expiration.HasValue)
            {
                InformationLog.Invoke("You have a valid license key for the Duende software {edition} edition for use at {licenseCompany}. The license expires on {licenseExpiration}.",
                    new object[] { License.Edition, License.CompanyName, License.Expiration.Value.ToLongDateString() });
            }
            else
            {
                InformationLog.Invoke(
                    "You have a valid license key for the Duende software {edition} edition for use at {licenseCompany}.",
                    new object[] { License.Edition, License.CompanyName });
            }
        }
    }

    protected virtual void ValidateExpiration(List<string> errors)
    {
        if (License.Expiration.HasValue)
        {
            var diff = DateTime.UtcNow.Date.Subtract(License.Expiration.Value.Date).TotalDays;
            if (diff > 0)
            {
                errors.Add($"Your license for the Duende software expired {diff} days ago.");
            }
        }
    }

    // this should just add to the error list
    protected virtual void ValidateProductFeatures(List<string> errors)
    {
    }

    // this should just write to the logs
    protected virtual void WarnForProductFeaturesWhenMissingLicense()
    {
    }

    internal T ValidateKey(string licenseKey)
    {
        if (!string.IsNullOrWhiteSpace(licenseKey))
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
                var license = new T();
                license.Initialize(new ClaimsPrincipal(validateResult.ClaimsIdentity));
                return license;
            }
            else
            {
                Logger.LogCritical(validateResult.Exception, "Error validating the Duende software license key");
            }
        }

        return null;
    }

    protected void LogToTrace(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Trace))
        {
            LoggerExtensions.LogTrace(Logger, message, args);
        }
    }

    protected void LogToDebug(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            LoggerExtensions.LogDebug(Logger, message, args);
        }
    }

    protected void LogToInformation(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Information))
        {
            LoggerExtensions.LogInformation(Logger, message, args);
        }
    }

    protected void LogToWarning(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Warning))
        {
            LoggerExtensions.LogWarning(Logger, message, args);
        }
    }

    protected void LogToError(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Error))
        {
            LoggerExtensions.LogError(Logger, message, args);
        }
    }
}
