// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Licensing;

/// <summary>
/// Exposes license metadata for display in UI templates and diagnostics.
/// </summary>
public sealed class LicenseInformation
{
    /// <summary>
    /// The company name from the license.
    /// </summary>
    public string? CompanyName { get; init; }

    /// <summary>
    /// The company contact info.
    /// </summary>
    public string? ContactInfo { get; init; }

    /// <summary>
    /// The license serial number.
    /// </summary>
    public int? SerialNumber { get; init; }

    /// <summary>
    /// When the license was issued.
    /// </summary>
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>
    /// When the license expires.
    /// </summary>
    public DateTimeOffset? Expiration { get; init; }

    /// <summary>
    /// True if a license was loaded and parsed successfully.
    /// </summary>
    public bool IsConfigured { get; init; }

    public IReadOnlyCollection<string> EntitledSkus { get; init; } = [];
}
