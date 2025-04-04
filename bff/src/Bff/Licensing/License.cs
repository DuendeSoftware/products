// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.Bff.Licensing;

internal class License
{
    // for testing
    internal License(params Claim[] claims)
        : this(new ClaimsPrincipal(new ClaimsIdentity(claims)))
    {
    }

    public License(ClaimsPrincipal claims)
    {
        if (int.TryParse(claims.FindFirst("id")?.Value, out var id))
        {
            Id = id;
        }

        CompanyName = claims.FindFirst("company_name")?.Value;
        ContactInfo = claims.FindFirst("contact_info")?.Value;

        if (long.TryParse(claims.FindFirst("exp")?.Value, out var exp))
        {
            var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
            Expiration = expDate.UtcDateTime;
        }

        var edition = claims.FindFirst("edition")?.Value;
        if (!Enum.TryParse<LicenseEdition>(edition, true, out var editionValue))
        {
            throw new Exception($"Invalid edition in license: '{edition}'");
        }

        Edition = editionValue;
        RedistributionFeature = claims.HasClaim("feature", "isv") || claims.HasClaim("feature", "redistribution");
        Extras = claims.FindFirst("extras")?.Value;

        if (IsCommunityEdition && RedistributionFeature)
        {
            throw new Exception("Invalid License: Redistribution is not valid for community edition.");
        }

        if (IsBffEdition && RedistributionFeature)
        {
            throw new Exception("Invalid License: Redistribution is not valid for BFF edition.");
        }

        if (IsBffEdition)
        {
            // for BFF-only edition we set BFF flag and ignore all other features
            BffFeature = true;
            ClientLimit = 0;
            IssuerLimit = 0;
            return;
        }

        KeyManagementFeature = claims.HasClaim("feature", "key_management");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Business:
            case LicenseEdition.Community:
                KeyManagementFeature = true;
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

        ConfigApiFeature = claims.HasClaim("feature", "config_api");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Business:
            case LicenseEdition.Community:
                ConfigApiFeature = true;
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

        BffFeature = claims.HasClaim("feature", "bff");
        switch (Edition)
        {
            case LicenseEdition.Enterprise:
            case LicenseEdition.Business:
            case LicenseEdition.Community:
                BffFeature = true;
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

    public int Id { get; set; }

    public string? CompanyName { get; set; }
    public string? ContactInfo { get; set; }

    public DateTime? Expiration { get; set; }

    public LicenseEdition Edition { get; set; }

    internal bool IsEnterpriseEdition => Edition == LicenseEdition.Enterprise;
    internal bool IsBusinessEdition => Edition == LicenseEdition.Business;
    internal bool IsStarterEdition => Edition == LicenseEdition.Starter;
    internal bool IsCommunityEdition => Edition == LicenseEdition.Community;
    internal bool IsBffEdition => Edition == LicenseEdition.Bff;

    public int? ClientLimit { get; set; }
    public int? IssuerLimit { get; set; }

    public bool KeyManagementFeature { get; set; }
    public bool ResourceIsolationFeature { get; set; }
    public bool DynamicProvidersFeature { get; set; }
    public bool RedistributionFeature { get; set; }
    public bool BffFeature { get; set; }
    public bool CibaFeature { get; set; }
    public bool ServerSideSessionsFeature { get; set; }
    public bool ConfigApiFeature { get; set; }
    public bool DPoPFeature { get; set; }

    public string? Extras { get; set; }

    public enum LicenseEdition
    {
        Enterprise,
        Business,
        Starter,
        Community,
        Bff
    }

    public override string ToString() => ObjectSerializer.ToString(this);

    internal static class ObjectSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string ToString(object o) => JsonSerializer.Serialize(o, Options);
    }
}
