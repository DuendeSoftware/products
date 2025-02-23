// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable disable

using System.Security.Claims;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Models the license for IdentityServer.
/// </summary>
public class ConfigurationLicense : License
{
    /// <summary>
    /// Ctor
    /// </summary>
    public ConfigurationLicense()
    {
    }

    // for testing
    internal ConfigurationLicense(params Claim[] claims)
    {
        Initialize(new ClaimsPrincipal(new ClaimsIdentity(claims)));
    }

    internal override void Initialize(ClaimsPrincipal claims)
    {
        base.Initialize(claims);

        ConfigApiFeature = claims.HasClaim("feature", "config_api");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Business:
            case LicenseEdition.Community:
                ConfigApiFeature = true;
                break;
        }
    }
    
    /// <summary>
    /// The Configuration API.
    /// </summary>
    public bool ConfigApiFeature { get; set; }
}
