// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Configuration;
using Duende.ConformanceReport.Endpoints;
using Duende.ConformanceReport.Licensing;
using Duende.ConformanceReport.Services;
using Duende.Private.Licencing.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport;

/// <summary>
/// Extension methods for adding conformance services to the DI container.
/// </summary>
internal static class ConformanceReportServiceCollectionExtensions
{
    /// <summary>
    /// Adds core conformance services to the service collection.
    /// </summary>
    internal static IServiceCollection AddConformanceReport(
        this IServiceCollection services,
        Action<ConformanceReportOptions>? configure = null)
    {
        _ = services.AddOptions<ConformanceReportOptions>();

        if (configure != null)
        {
            _ = services.Configure(configure);
        }

        // Register HTTP context accessor if not already registered
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.TryAddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<V2LicenseAccessor>>();
            var accessor = new V2LicenseAccessor(() => null, logger);
            return accessor.Current;
        });
        services.TryAddSingleton<LicenseValidator>();
        services.TryAddSingleton<ConformanceReportLicenseValidator>();

        // Register assessment service
        _ = services.AddTransient<ConformanceReportAssessmentService>();

        // Register endpoint
        _ = services.AddTransient<ConformanceReportEndpoint>();

        // Register authorization policy configuration
        _ = services.AddSingleton<IConfigureOptions<AuthorizationOptions>,
            ConfigureConformanceReportAuthorizationPolicy>();

        return services;
    }
}
