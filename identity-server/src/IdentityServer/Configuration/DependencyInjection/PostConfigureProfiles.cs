// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Post-configures IdentityServerOptions based on enabled configuration profiles.
/// Configuration profiles allow developers to express the intention that they are following a particular specification or profile,
/// such as OAuth 2.1, FAPI 2.0, etc. This class automatically configures options to comply with the enabled profiles.
/// </summary>
public class PostConfigureProfiles : IPostConfigureOptions<IdentityServerOptions>
{
    private readonly IEnumerable<IConfigurationProfile> _profiles;
    private readonly ILogger<PostConfigureProfiles> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostConfigureProfiles"/> class.
    /// </summary>
    /// <param name="profiles">The collection of configuration profiles.</param>
    /// <param name="logger">The logger</param>
    public PostConfigureProfiles(IEnumerable<IConfigurationProfile> profiles, ILogger<PostConfigureProfiles> logger)
    {
        _profiles = profiles;
        _logger = logger;
    }

    /// <inheritdoc />
    public void PostConfigure(string? name, IdentityServerOptions options)
    {
        foreach (var enabledProfile in options.ConfigurationProfiles.EnabledProfiles)
        {
            var profile = _profiles.FirstOrDefault(s =>
                s.ProfileName.Equals(enabledProfile, StringComparison.OrdinalIgnoreCase));

            // TODO - Consider what to do with the result (e.g., logging, validation, etc.)
            if (profile == null)
            {
                _logger.LogWarning("No IConfigurationProfile found for enabled profile: {ProfileName}", enabledProfile);
            }
            else
            {
                _ = profile.ApplyProfile(options);
            }
        }
    }
}
