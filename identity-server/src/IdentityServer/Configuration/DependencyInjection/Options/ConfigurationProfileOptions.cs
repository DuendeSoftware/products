// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for configuration profiles.
/// Configuration profiles allow developers to express the intention that they are following a particular specification or profile,
/// such as OAuth 2.1, FAPI 2.0, etc. IdentityServer will automatically configure options and validate client configuration to
/// comply with the profile.
/// </summary>
public class ConfigurationProfileOptions
{
    /// <summary>
    /// Gets or sets the collection of active configuration profiles.
    /// Multiple profiles can be active simultaneously.
    /// Use constants from <see cref="IdentityServerConstants.ConfigurationProfiles"/> for well-known profiles,
    /// or specify custom profile identifiers.
    /// Defaults to an empty collection (no profiles active).
    /// </summary>
    public ICollection<string> EnabledProfiles { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets whether to log informational messages when a profile overrides a configuration setting.
    /// This helps developers understand when their explicit configuration is being changed to comply with a profile.
    /// Defaults to true.
    /// </summary>
    public bool LogProfileOverrides { get; set; } = true;
}
