// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for configuration profiles.
/// </summary>
public class ConfigurationProfileOptions
{
    /// <summary>
    /// Gets or sets the collection of active configuration profiles.
    /// </summary>
    public ICollection<string> Profiles { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether to log informational messages when a profile overrides a configuration setting.
    /// </summary>
    public bool LogProfileOverrides { get; set; }
}
