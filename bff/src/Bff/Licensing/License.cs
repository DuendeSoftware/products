// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace Duende.Bff.Licensing;

/// <summary>
/// Models a Duende commercial license.
/// </summary>
internal class License
{
    /// <summary>
    /// Initializes the license from the claims in a key.
    /// </summary>
    internal License(ClaimsPrincipal claims)
    {
        if (int.TryParse(claims.FindFirst(LicenseClaimTypes.Id)?.Value, out var id))
        {
            SerialNumber = id;
        }

        CompanyName = claims.FindFirst(LicenseClaimTypes.CompanyName)?.Value;
        ContactInfo = claims.FindFirst(LicenseClaimTypes.ContactInfo)?.Value;

        if (long.TryParse(claims.FindFirst(LicenseClaimTypes.Expiration)?.Value, out var exp))
        {
            Expiration = DateTimeOffset.FromUnixTimeSeconds(exp);
        }

        // IsConfigured needs to be set prior to checking for clients and issuers claims or the Redistribution check will not return an appropriate value
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
        nameof(Extras))
    ]
    public bool IsConfigured { get; init; }
}
