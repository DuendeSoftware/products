// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Applies FAPI 2.0 Security Profile configuration to IdentityServerOptions.
/// When this profile is active, IdentityServer enforces FAPI 2.0 requirements.
/// </summary>
public class Fapi2ProfileService : IConfigurationProfileService
{
    private readonly ILogger<Fapi2ProfileService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Fapi2ProfileService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public Fapi2ProfileService(ILogger<Fapi2ProfileService> logger) => _logger = logger;

    /// <inheritdoc />
    public string ProfileName => ConfigurationProfiles.Fapi2;

    /// <inheritdoc />
    public void ApplyProfile(IdentityServerOptions options)
    {
        if (options.ConfigurationProfile.LogProfileOverrides)
        {
            _logger.LogInformation("The fapi2 configuration profile is setting PushedAuthorization.Required to true.");
        }

        options.PushedAuthorization.Required = true;
    }

    /// <inheritdoc />
    public void ValidateClient(IdentityServerOptions options, Validation.ClientConfigurationValidationContext context)
    {
        // Placeholder for future FAPI 2.0 client-specific validation logic.
        // Intentionally left blank in initial skeleton implementation.
    }
}
