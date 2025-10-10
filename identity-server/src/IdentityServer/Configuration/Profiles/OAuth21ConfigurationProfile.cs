// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// Applies OAuth 2.1 Profile configuration to IdentityServerOptions.
/// When this profile is active, IdentityServer enforces OAuth 2.1 requirements.
/// </summary>
public class OAuth21ConfigurationProfile : IConfigurationProfile
{
    private readonly ILogger<OAuth21ConfigurationProfile> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth21ConfigurationProfile"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OAuth21ConfigurationProfile(ILogger<OAuth21ConfigurationProfile> logger) => _logger = logger;

    /// <inheritdoc />
    public string ProfileName => IdentityServerConstants.ConfigurationProfiles.OAuth21;

    /// <inheritdoc />
    public ProfileValidationResult ApplyProfile(IdentityServerOptions options)
    {
        var result = new ProfileValidationResult();

        // Placeholder for future OAuth 2.1 options validation logic.
        // Intentionally left blank in initial skeleton implementation.

        return result;
    }

    /// <inheritdoc />
    public ProfileValidationResult ValidateClient(IdentityServerOptions options, Validation.ClientConfigurationValidationContext context)
    {
        var result = new ProfileValidationResult();

        // Placeholder for future OAuth 2.1 client-specific validation logic.
        // Intentionally left blank in initial skeleton implementation.

        return result;
    }
}
