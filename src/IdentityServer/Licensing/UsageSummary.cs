// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Duende.IdentityServer.Licensing;

/// <summary>
/// Usage summary for the current license.
/// </summary>
/// <param name="LicenseEdition"></param>
/// <param name="ClientsUsed"></param>
/// <param name="IssuersUsed"></param>
/// <param name="FeaturesUsed"></param>
public record LicenseUsageSummary(
    string LicenseEdition,
    IReadOnlyCollection<string> ClientsUsed,
    IReadOnlyCollection<string> IssuersUsed,
    IReadOnlyCollection<string> FeaturesUsed);