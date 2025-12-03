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
internal class LicenseValidator<T>(TimeProvider clock)
    where T : License, new()
{
    private readonly TimeProvider _clock = clock;

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
        //if (Logger != null) throw new InvalidOperationException("LicenseValidator already initialized.");

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
                          "Please start a conversation with us: https://duende.link/l/contact";

            // we're not using our _warningLog because we always want this emitted regardless of the context
#pragma warning disable CA2254 // Structured logging is not needed for this message
            Logger.LogWarning(message);
#pragma warning restore CA2254
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
            var diff = _clock.GetUtcNow().Date.Subtract(License.Expiration.Value.Date).TotalDays;
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
#pragma warning disable CA5404 // This is validated elsewhere
                ValidateLifetime = false
#pragma warning restore CA5404
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
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogTrace(Logger, message, args);
#pragma warning restore CA2254
        }
    }

    protected void LogToDebug(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogDebug(Logger, message, args);
#pragma warning restore CA2254
        }
    }

    protected void LogToInformation(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Information))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogInformation(Logger, message, args);
#pragma warning restore CA2254
        }
    }

    protected void LogToWarning(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Warning))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogWarning(Logger, message, args);
#pragma warning restore CA2254
        }
    }

    protected void LogToError(string message, params object[] args)
    {
        if (Logger.IsEnabled(LogLevel.Error))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogError(Logger, message, args);
#pragma warning restore CA2254
        }
    }
}
