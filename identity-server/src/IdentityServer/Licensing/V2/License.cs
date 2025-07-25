// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace Duende.IdentityServer.Licensing.V2;

/// <summary>
/// Models a Duende commercial license.
/// </summary>
internal class License
{
    /// <summary>
    /// Initializes an empty (non-configured) license.
    /// </summary>
    internal License()
    {
    }

    /// <summary>
    /// Initializes the license from the claims in a key.
    /// </summary>
    internal License(ClaimsPrincipal claims)
    {
        if (int.TryParse(claims.FindFirst("id")?.Value, out var id))
        {
            SerialNumber = id;
        }

        CompanyName = claims.FindFirst("company_name")?.Value;
        ContactInfo = claims.FindFirst("contact_info")?.Value;

        if (long.TryParse(claims.FindFirst("exp")?.Value, out var exp))
        {
            Expiration = DateTimeOffset.FromUnixTimeSeconds(exp);
        }

        var edition = claims.FindFirstValue("edition");
        if (edition != null)
        {
            if (!Enum.TryParse<LicenseEdition>(edition, true, out var editionValue))
            {
                throw new Exception($"Invalid edition in license: '{edition}'");
            }

            Edition = editionValue;
        }

        Features = claims.FindAll("feature").Select(f => f.Value).ToArray();

        Extras = claims.FindFirst("extras")?.Value ?? string.Empty;

        // IsConfigured needs to be set prior to checking for clients and issuers claims or the Redistribution check will not return an appropriate value
        IsConfigured = true;

        if (!claims.HasClaim("feature", "unlimited_clients"))
        {
            // default values
            if (Redistribution)
            {
                // default for all ISV editions
                ClientLimit = 5;
            }
            else
            {
                // defaults limits for non-ISV editions
                ClientLimit = Edition switch
                {
                    LicenseEdition.Business => 15,
                    LicenseEdition.Starter => 5,
                    _ => ClientLimit
                };
            }

            if (int.TryParse(claims.FindFirst("client_limit")?.Value, out var clientLimit))
            {
                // explicit, so use that value
                ClientLimit = clientLimit;
            }

            if (!Redistribution)
            {
                // these for the non-ISV editions that always have unlimited, regardless of explicit value
                ClientLimit = Edition switch
                {
                    LicenseEdition.Enterprise or LicenseEdition.Community =>
                        // unlimited
                        null,
                    _ => ClientLimit
                };
            }
        }

        if (!claims.HasClaim("feature", "unlimited_issuers"))
        {
            // default
            IssuerLimit = 1;

            if (int.TryParse(claims.FindFirst("issuer_limit")?.Value, out var issuerLimit))
            {
                IssuerLimit = issuerLimit;
            }

            // these for the editions that always have unlimited, regardless of explicit value
            IssuerLimit = Edition switch
            {
                LicenseEdition.Enterprise or LicenseEdition.Community =>
                    // unlimited
                    null,
                _ => IssuerLimit
            };
        }
    }

    /// <summary>
    /// The serial number
    /// </summary>
    public int? SerialNumber { get; init; }

    /// <summary>
    /// The company name
    /// </summary>
    public string? CompanyName { get; init; }

    /// <summary>
    /// The company contact info
    /// </summary>
    public string? ContactInfo { get; init; }

    /// <summary>
    /// The license expiration
    /// </summary>
    public DateTimeOffset? Expiration { get; init; }

    /// <summary>
    /// The license edition
    /// </summary>
    public LicenseEdition? Edition { get; init; }

    /// <summary>
    /// True if redistribution is enabled for this license, and false otherwise.
    /// </summary>
    public bool Redistribution => IsConfigured && (IsEnabled(LicenseFeature.Redistribution) || IsEnabled(LicenseFeature.ISV));

    /// <summary>
    /// The number of clients this license allows, or <c>null</c> if the license allows unlimited clients.
    /// </summary>
    public int? ClientLimit { get; init; }

    /// <summary>
    /// The number of issuers this license allows, or <c>null</c> if the license allows unlimited issuers.
    /// </summary>
    public int? IssuerLimit { get; init; }

    /// <summary>
    /// The license features
    /// </summary>
    public string[] Features { get; init; } = [];

    /// <summary>
    /// Extras
    /// </summary>
    public string? Extras { get; init; }

    /// <summary>
    /// True if the license was configured in options or from a file, and false otherwise.
    /// </summary>
    [MemberNotNullWhen(true,
        nameof(SerialNumber),
        nameof(CompanyName),
        nameof(ContactInfo),
        nameof(Expiration),
        nameof(Edition),
        nameof(Extras))
    ]
    public bool IsConfigured { get; init; }

    /// <summary>
    /// Checks if a LicenseFeature is enabled in the current license. If there
    /// is no configured license, this always returns true.
    /// </summary>
    /// <param name="feature"></param>
    /// <returns></returns>
    public bool IsEnabled(LicenseFeature feature) => !IsConfigured || (AllowedFeatureMask & (ulong)feature) != 0;


    private ulong? _allowedFeatureMask;
    private ulong AllowedFeatureMask
    {
        get
        {
            if (_allowedFeatureMask == null)
            {
                var features = FeatureMaskForEdition();
                foreach (var featureClaim in Features)
                {
                    var feature = ToFeatureEnum(featureClaim);
                    features |= (ulong)feature;
                }

                _allowedFeatureMask = features;
            }
            return _allowedFeatureMask.Value;
        }
    }

    private static LicenseFeature ToFeatureEnum(string claimValue)
    {
        foreach (var field in typeof(LicenseFeature).GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (string.Equals(attribute.Description, claimValue, StringComparison.OrdinalIgnoreCase))
                {
                    return (LicenseFeature)field.GetValue(null)!;
                }
            }
        }
        throw new ArgumentException("Unknown license feature {feature}", claimValue);
    }


    private ulong FeatureMaskForEdition() => Edition switch
    {
        null => FeatureMaskForFeatures(),
        LicenseEdition.Bff => FeatureMaskForFeatures(),
        LicenseEdition.Starter => FeatureMaskForFeatures(),
        LicenseEdition.Business => FeatureMaskForFeatures(
            LicenseFeature.KeyManagement,
            LicenseFeature.PAR,
            LicenseFeature.ServerSideSessions,
            LicenseFeature.DCR),
        LicenseEdition.Enterprise => FeatureMaskForFeatures(
            LicenseFeature.KeyManagement,
            LicenseFeature.PAR,
            LicenseFeature.ResourceIsolation,
            LicenseFeature.DynamicProviders,
            LicenseFeature.CIBA,
            LicenseFeature.ServerSideSessions,
            LicenseFeature.DPoP,
            LicenseFeature.DCR
        ),
        LicenseEdition.Community => FeatureMaskForFeatures(
            LicenseFeature.KeyManagement,
            LicenseFeature.PAR,
            LicenseFeature.ResourceIsolation,
            LicenseFeature.DynamicProviders,
            LicenseFeature.CIBA,
            LicenseFeature.ServerSideSessions,
            LicenseFeature.DPoP,
            LicenseFeature.DCR
        ),
        _ => throw new ArgumentException(),
    };

    private static ulong FeatureMaskForFeatures(params LicenseFeature[] licenseFeatures)
    {
        var result = 0UL;
        foreach (var feature in licenseFeatures)
        {
            result |= (ulong)feature;
        }
        return result;
    }
}
