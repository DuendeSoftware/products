// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Licensing;

/// <summary>
/// Usage summary for the current IdentityServer instance intended for auditing purposes.
/// </summary>
/// <param name="EntitledSkus">SKU identifiers entitled by the configured license.</param>
/// <param name="ClientsUsed">Clients used in the current IdentityServer instance.</param>
/// <param name="IssuersUsed">Issuers used in the current IdentityServer instance.</param>
/// <param name="FeaturesUsed">Features used in the current IdentityServer instance.</param>
public record LicenseUsageSummary(
    IReadOnlyCollection<string> EntitledSkus,
    IReadOnlyCollection<string> ClientsUsed,
    IReadOnlyCollection<string> IssuersUsed,
    IReadOnlyCollection<string> FeaturesUsed);
