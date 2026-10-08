// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.ConformanceReport.Licensing;

/// <summary>
/// Conformance report license validation. Delegates to the shared <see cref="LicenseValidator"/>
/// infrastructure for rate-limited logging and entitlement checks.
/// </summary>
internal sealed class ConformanceReportLicenseValidator(LicenseValidator validator)
{
    internal bool ValidateConformanceReport() => validator.ValidateFeature(SkuIds.IS_003);

    internal static ConformanceReportLicenseValidator CreateForTests()
    {
        var v2License = new V2LicenseAccessor(static () => null, NullLogger<V2LicenseAccessor>.Instance).Current;
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        return new ConformanceReportLicenseValidator(
            new LicenseValidator(v2License, NullLogger<LicenseValidator>.Instance, TimeProvider.System, configuration));
    }
}
