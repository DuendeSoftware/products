// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// Models a Duende commercial license.
/// </summary>
public sealed class License
{
    
    /// <summary>
    /// Initializes an empty (non-configured) license.
    /// </summary>
    internal License()
    {
        IsConfigured = false;
        Features = [];
    }

    /// <summary>
    /// Initializes the license from the claims in a key.
    /// </summary>
    internal License(ClaimsPrincipal claims)
    {
        if (Int32.TryParse(claims.FindFirst("id")?.Value, out var id))
        {
            SerialNumber = id;
        }

        CompanyName = claims.FindFirst("company_name")?.Value;
        ContactInfo = claims.FindFirst("contact_info")?.Value;

        if (Int64.TryParse(claims.FindFirst("exp")?.Value, out var exp))
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


        Features = claims.FindAll("feature").Select(f => f.Value);
        
        Extras = claims.FindFirst("extras")?.Value ?? string.Empty;
        IsConfigured = true;
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
    public bool Redistribution => IsEnabled(LicenseFeature.Redistribution) || IsEnabled(LicenseFeature.ISV); 
    
    /// <summary>
    /// The license features
    /// </summary>
    public IEnumerable<string> Features { get; init; }
    
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
    public bool IsEnabled(LicenseFeature feature)
    {
        return !IsConfigured || (AllowedFeatureMask & feature.ToFeatureMask()) != 0;
    }

    
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
                    features |= feature.ToFeatureMask();
                }

                _allowedFeatureMask = features;
            }
            return _allowedFeatureMask.Value;
        }
    }
    
    private LicenseFeature ToFeatureEnum(string claimValue)
    {
        foreach(var field in typeof(LicenseFeature).GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (string.Equals(attribute.Description, claimValue, StringComparison.OrdinalIgnoreCase))
                {
                    return (LicenseFeature) field.GetValue(null)!;
                }
            }
        }
        throw new ArgumentException("Unknown license feature {feature}", claimValue);
    }
    
    
    private ulong FeatureMaskForEdition()
    {
        return Edition switch
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
    }

    private ulong FeatureMaskForFeatures(params IEnumerable<LicenseFeature> licenseFeatures)
    {
        var result = 0UL;
        foreach(var feature in licenseFeatures)
        {
            result |= feature.ToFeatureMask();
        }
        return result;
    }
}