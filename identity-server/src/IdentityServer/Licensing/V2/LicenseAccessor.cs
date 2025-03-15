// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using System.Security.Cryptography;
using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Licensing.V2;

/// <summary>
/// Loads the license from configuration or a file, and validates its contents.
/// </summary>
internal class LicenseAccessor(IdentityServerOptions options, ILogger<LicenseAccessor> logger)
{
    private static readonly string[] LicenseFileNames =
    [
        "Duende_License.key",
        "Duende_IdentityServer_License.key",
    ];

    private License? _license;
    private readonly object _lock = new();

    public License Current => _license ??= Initialize();

    private License Initialize()
    {
        lock (_lock)
        {
            if (_license != null)
            {
                return _license;
            }

            var key = options.LicenseKey ?? LoadLicenseKeyFromFile();
            if (key == null)
            {
                return new License();
            }

            var licenseClaims = ValidateKey(key);
            return licenseClaims.Any() ? // (ValidateKey will return an empty collection if it fails)
                new License(new ClaimsPrincipal(new ClaimsIdentity(licenseClaims))) : new License();
        }
    }

    private static string? LoadLicenseKeyFromFile()
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

    private Claim[] ValidateKey(string licenseKey)
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
        if (!validateResult.IsValid)
        {
            logger.LogCritical(validateResult.Exception, "Error validating the Duende software license key");
        }

        return validateResult.ClaimsIdentity?.Claims.ToArray() ?? [];
    }

}
