// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Duende.IdentityServer.Licensing;

/// <summary>
/// Usage summary for the current IdentityServer instance intended for auditing purposes.
/// </summary>
/// <param name="LicenseEdition">License edition retrieved from license key.</param>
/// <param name="ClientsUsed">Clients used in the current IdentityServer instance.</param>
/// <param name="IssuersUsed">Issuers used in the current IdentityServer instance.</param>
/// <param name="FeaturesUsed">Features used in the current IdentityServer instance.</param>
public record LicenseUsageSummary(
    string LicenseEdition,
    IReadOnlyCollection<string> ClientsUsed,
    IReadOnlyCollection<string> IssuersUsed,
    IReadOnlyCollection<string> FeaturesUsed);