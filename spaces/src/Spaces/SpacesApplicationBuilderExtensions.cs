// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Spaces.Internal.DataProtection;
using Duende.Spaces.Internal.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to add Spaces resolution middleware.
/// </summary>
public static class SpacesApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="SpaceResolutionMiddleware"/> to the application pipeline.
    /// </summary>
    /// <remarks>
    /// Call this before <c>UseAuthentication()</c> and any middleware that uses data protection. When
    /// <see cref="SpacesOptions.PartitionDataProtection"/> is enabled it also verifies that the app's
    /// <c>IDataProtectionProvider</c> is still the partitioned one.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// The license does not include Spaces, or data protection partitioning is enabled but the app's
    /// <c>IDataProtectionProvider</c> is not partitioned.
    /// </exception>
    public static IApplicationBuilder UseSpaceResolution(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var licenseValidator = app.ApplicationServices.GetRequiredService<SpacesLicenseValidator>();
        if (!licenseValidator.ValidateSpaces())
        {
            SpacesLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Spaces feature.");
        }

        SpacesDataProtectionValidation.Validate(app.ApplicationServices);

        return app.UseMiddleware<SpaceResolutionMiddleware>();
    }
}
