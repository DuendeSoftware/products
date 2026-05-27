// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Licensing.Enforcement;

/// <summary>
/// Extension methods to register the licensing enforcement infrastructure.
/// </summary>
internal static class LicensingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Duende licensing enforcement services.
    /// </summary>
    internal static IServiceCollection AddDuendeLicensing(this IServiceCollection services)
    {
        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LicenseOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<V2LicenseAccessor>>();
            var accessor = new V2LicenseAccessor(() => ResolveLicenseKey(options), logger);
            return accessor.Current;
        });

        services.TryAddSingleton<LicenseValidator>();

        return services;
    }

    private static string? ResolveLicenseKey(LicenseOptions options)
    {
        if (!string.IsNullOrEmpty(options.LicenseKey))
        {
            return options.LicenseKey;
        }

        if (!string.IsNullOrEmpty(options.LicenseKeyPath) && File.Exists(options.LicenseKeyPath))
        {
            try
            {
                return File.ReadAllText(options.LicenseKeyPath).Trim();
            }
            catch (IOException)
            {
                // Treat file read failures as "no license configured".
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }
}
