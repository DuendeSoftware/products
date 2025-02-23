// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable disable

using System;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende;

/// <summary>
/// Models the license for IdentityServer.
/// </summary>
public abstract class License
{
    /// <summary>
    /// Ctor
    /// </summary>
    protected License()
    {
    }

    /// <summary>
    /// Initializes the license from the claims in the key.
    /// </summary>
    internal virtual void Initialize(ClaimsPrincipal claims)
    {
        Claims = claims;

        if (Int32.TryParse(claims.FindFirst("id")?.Value, out var id))
        {
            SerialNumber = id;
        }

        CompanyName = claims.FindFirst("company_name")?.Value;
        ContactInfo = claims.FindFirst("contact_info")?.Value;

        if (Int64.TryParse(claims.FindFirst("exp")?.Value, out var exp))
        {
            var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
            Expiration = expDate.UtcDateTime;
        }

        var edition = claims.FindFirst("edition")?.Value;
        if (!Enum.TryParse<License.LicenseEdition>(edition, true, out var editionValue))
        {
            throw new Exception($"Invalid edition in license: '{edition}'");
        }
        Edition = editionValue;

        Extras = claims.FindFirst("extras")?.Value;
    }

    internal ClaimsPrincipal Claims { get; private set; }

    /// <summary>
    /// The serial number
    /// </summary>
    public int SerialNumber { get; set; }

    /// <summary>
    /// The company name
    /// </summary>
    public string CompanyName { get; set; }
    /// <summary>
    /// The company contact info
    /// </summary>
    public string ContactInfo { get; set; }

    /// <summary>
    /// The license expiration
    /// </summary>
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// The license edition 
    /// </summary>
    public LicenseEdition Edition { get; set; }

    internal bool IsEnterpriseEdition => Edition == LicenseEdition.Enterprise;
    internal bool IsBusinessEdition => Edition == LicenseEdition.Business;
    internal bool IsStarterEdition => Edition == LicenseEdition.Starter;
    internal bool IsCommunityEdition => Edition == LicenseEdition.Community;
    internal bool IsBffEdition => Edition == LicenseEdition.Bff;

    /// <summary>
    /// Extras
    /// </summary>
    public string Extras { get; set; }

    /// <summary>
    /// Models the license tier
    /// </summary>
    public enum LicenseEdition
    {
        /// <summary>
        /// Enterprise
        /// </summary>
        Enterprise,
        /// <summary>
        /// Business
        /// </summary>
        Business,
        /// <summary>
        /// Starter
        /// </summary>
        Starter,
        /// <summary>
        /// Community
        /// </summary>
        Community,
        /// <summary>
        /// Bff
        /// </summary>
        Bff
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return ObjectSerializer.ToString(this);
    }

    internal static class ObjectSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string ToString(object o)
        {
            return JsonSerializer.Serialize(o, Options);
        }
    }
}
