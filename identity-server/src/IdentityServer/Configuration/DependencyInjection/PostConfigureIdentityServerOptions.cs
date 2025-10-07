// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Post-configures IdentityServerOptions based on enabled configuration profiles.
/// Configuration profiles allow developers to express the intention that they are following a particular specification or profile,
/// such as OAuth 2.1, FAPI 2.0, etc. This class automatically configures options to comply with the enabled profiles.
/// </summary>
public class PostConfigureIdentityServerOptions : IPostConfigureOptions<IdentityServerOptions>
{
    private readonly IEnumerable<IConfigurationProfileService> _profileServices;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostConfigureIdentityServerOptions"/> class.
    /// </summary>
    /// <param name="profileServices">The collection of profile services.</param>
    public PostConfigureIdentityServerOptions(IEnumerable<IConfigurationProfileService> profileServices) => _profileServices = profileServices;

    /// <inheritdoc />
    public void PostConfigure(string? name, IdentityServerOptions options)
    {
        foreach (var enabledProfile in options.ConfigurationProfile.Profiles)
        {
            var service = _profileServices.FirstOrDefault(s =>
                s.ProfileName.Equals(enabledProfile, StringComparison.OrdinalIgnoreCase));

            // TODO - Handle the case that no service is found for the enabled profile by logging a warning
            service?.ApplyProfile(options);
        }
    }
}
