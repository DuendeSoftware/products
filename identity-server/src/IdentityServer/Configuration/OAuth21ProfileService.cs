// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Applies OAuth 2.1 Profile configuration to IdentityServerOptions.
/// When this profile is active, IdentityServer enforces OAuth 2.1 requirements.
/// </summary>
public class OAuth21ProfileService : IConfigurationProfileService
{
    private readonly ILogger<OAuth21ProfileService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth21ProfileService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OAuth21ProfileService(ILogger<OAuth21ProfileService> logger) => _logger = logger;

    /// <inheritdoc />
    public string ProfileName => ConfigurationProfiles.OAuth21;

    /// <inheritdoc />
    public void ApplyProfile(IdentityServerOptions options)
    {
        if (options.ConfigurationProfile.LogProfileOverrides)
        {
            _logger.LogInformation("The oauth21 configuration profile is setting PushedAuthorization.Required to true.");
        }

        options.PushedAuthorization.Required = true;
    }

    /// <inheritdoc />
    public void ValidateClient(IdentityServerOptions options, Validation.ClientConfigurationValidationContext context)
    {
        // Placeholder for future OAuth 2.1 client-specific validation logic.
        // Intentionally left blank in initial skeleton implementation.
    }
}
