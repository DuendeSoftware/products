// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.ConformanceReport;

/// <summary>
/// Extension methods for adding conformance to IdentityServer.
/// </summary>
public static class IdentityServerBuilderExtensions
{
    /// <summary>
    /// Adds conformance assessment to IdentityServer.
    /// </summary>
    public static IIdentityServerBuilder AddConformanceReport(
        this IIdentityServerBuilder builder,
        Action<ConformanceReportOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        // Add core conformance services
        _ = services.AddConformanceReport(configure);

        // Register the server options provider that adapts IdentityServerOptions
        services.TryAddScoped<Func<ConformanceReportServerOptions>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<IdentityServerOptions>>().Value;
            return options.ToConformanceReportServerOptions;
        });

        // Register license info from IdentityServer license
        services.TryAddSingleton(sp =>
        {
            var licenseInformation = sp.GetRequiredService<LicenseInformation>();
            return ToConformanceReportLicenseInfo(licenseInformation);
        });

        // Register client store adapter
        services.TryAddScoped<IConformanceReportClientStore, IdentityServerClientStore>();

        return builder;
    }

    internal static ConformanceReportLicenseInfo ToConformanceReportLicenseInfo(
        LicenseInformation licenseInformation) =>
        new()
        {
            CompanyName = licenseInformation.CompanyName,
            ContactInfo = licenseInformation.ContactInfo,
            Expiration = licenseInformation.Expiration?.UtcDateTime,
            Edition = licenseInformation.EntitledSkus.Count > 0
                ? string.Join(", ", licenseInformation.EntitledSkus)
                : null
        };
}
