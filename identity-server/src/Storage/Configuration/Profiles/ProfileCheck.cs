// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// Represents the result of validating a single option or property in a configuration profile.
/// </summary>
public class ProfileCheck
{
    /// <summary>
    /// Gets or sets the path to the option or property being checked. Properties of clients are
    /// written as "Client.PropertyName". Options from IdentityServerOptions are written with their full path, e.g. "Endpoints.EnableTokenEndpoint".
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a description of the check or reason for failure.
    /// Failed checks use this to explain what needs to be corrected. Passed checks may leave this empty.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the profile overrode the user's configuration.
    /// When true, the property had a non-default value that violated profile requirements,
    /// and the profile replaced it with a compliant value.
    /// </summary>
    public bool WasOverridden { get; set; }
}
