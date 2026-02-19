// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport;
using Duende.IdentityServer.Configuration;
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
            return () => options.ToConformanceReportServerOptions();
        });

        // Register license info from IdentityServer license
        _ = services.AddScoped(sp =>
        {
            var license = sp.GetService<IdentityServerLicense>();
            return license?.ToConformanceReportLicenseInfo() ?? new ConformanceReportLicenseInfo();
        });

        // Register client store adapter
        services.TryAddScoped<IConformanceReportClientStore, IdentityServerClientStore>();

        return builder;
    }

    /// <summary>
    /// Converts an IdentityServerLicense to ConformanceReportLicenseInfo.
    /// </summary>
    internal static ConformanceReportLicenseInfo ToConformanceReportLicenseInfo(this IdentityServerLicense license) =>
        new()
        {
            CompanyName = license.CompanyName,
            ContactInfo = license.ContactInfo,
            SerialNumber = license.SerialNumber,
            Expiration = license.Expiration,
            Edition = license.Edition.ToString()
        };
}
