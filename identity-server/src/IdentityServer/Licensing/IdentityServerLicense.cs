// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable disable

using System.Security.Claims;

namespace Duende.IdentityServer;

/// <summary>
/// Models the license for IdentityServer.
/// </summary>
public class IdentityServerLicense : License
{
    /// <summary>
    /// Ctor
    /// </summary>
    public IdentityServerLicense()
    {
    }

    // for testing
    internal IdentityServerLicense(params Claim[] claims) => Initialize(new ClaimsPrincipal(new ClaimsIdentity(claims)));

    internal override void Initialize(ClaimsPrincipal claims)
    {
        base.Initialize(claims);

        RedistributionFeature = claims.HasClaim("feature", "isv") || claims.HasClaim("feature", "redistribution");

        KeyManagementFeature = claims.HasClaim("feature", "key_management");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Business:
            case LicenseEdition.Community:
                KeyManagementFeature = true;
                break;
        }

        ParFeature = claims.HasClaim("feature", "par");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Business:
            case LicenseEdition.Community:
                ParFeature = true;
                break;
        }

        ResourceIsolationFeature = claims.HasClaim("feature", "resource_isolation");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Community:
                ResourceIsolationFeature = true;
                break;
        }

        DynamicProvidersFeature = claims.HasClaim("feature", "dynamic_providers");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Community:
                DynamicProvidersFeature = true;
                break;
        }

        CibaFeature = claims.HasClaim("feature", "ciba");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Community:
                CibaFeature = true;
                break;
        }

        ServerSideSessionsFeature = claims.HasClaim("feature", "server_side_sessions");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Business:
            case LicenseEdition.Community:
                ServerSideSessionsFeature = true;
                break;
        }

        DPoPFeature = claims.HasClaim("feature", "dpop");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Community:
                DPoPFeature = true;
                break;
        }

        if (!claims.HasClaim("feature", "unlimited_clients"))
        {
            // default values
            if (RedistributionFeature)
            {
                // default for all ISV editions
                ClientLimit = 5;
            }
            else
            {
                // defaults limits for non-ISV editions
                switch (Edition)
                {
                    case LicenseEdition.Business:
                        ClientLimit = 15;
                        break;
                    case LicenseEdition.Starter:
                        ClientLimit = 5;
                        break;
                }
            }

            if (int.TryParse(claims.FindFirst("client_limit")?.Value, out var clientLimit))
            {
                // explicit, so use that value
                ClientLimit = clientLimit;
            }

            if (!RedistributionFeature)
            {
                // these for the non-ISV editions that always have unlimited, regardless of explicit value
                switch (Edition)
                {
                    case LicenseEdition.Enterprise:
                    case LicenseEdition.Community:
                        // unlimited
                        ClientLimit = null;
                        break;
                }
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
            switch (Edition)
            {
                case LicenseEdition.Enterprise:
                case LicenseEdition.Community:
                    // unlimited
                    IssuerLimit = null;
                    break;
            }
        }
    }

    /// <summary>
    /// The client limit
    /// </summary>
    public int? ClientLimit { get; set; }
    /// <summary>
    /// The issuer limit
    /// </summary>
    public int? IssuerLimit { get; set; }

    /// <summary>
    /// Automatic key management
    /// </summary>
    public bool KeyManagementFeature { get; set; }
    /// <summary>
    /// PAR
    /// </summary>
    public bool ParFeature { get; set; }
    /// <summary>
    /// Resource isolation
    /// </summary>
    public bool ResourceIsolationFeature { get; set; }
    /// <summary>
    /// Dynamic providers
    /// </summary>
    public bool DynamicProvidersFeature { get; set; }
    /// <summary>
    /// Redistribution
    /// </summary>
    public bool RedistributionFeature { get; set; }
    /// <summary>
    /// CIBA
    /// </summary>
    public bool CibaFeature { get; set; }
    /// <summary>
    /// Server-side sessions
    /// </summary>
    public bool ServerSideSessionsFeature { get; set; }
    /// <summary>
    /// DPoP
    /// </summary>
    public bool DPoPFeature { get; set; }
}
