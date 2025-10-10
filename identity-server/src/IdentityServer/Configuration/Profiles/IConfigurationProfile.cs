// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

#nullable enable

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// Defines the behavior for applying a specific configuration profile to <see cref="IdentityServerOptions"/> and <see cref="Client"/> configuration.
/// Implementations of this interface handle the logic for a particular profile (e.g., FAPI 2.0, OAuth 2.1).
/// </summary>
public interface IConfigurationProfile
{
    /// <summary>
    /// Gets the name of the profile that this service handles.
    /// This should match the constants in <see cref="IdentityServerConstants.ConfigurationProfiles"/>.
    /// </summary>
    string ProfileName { get; }

    /// <summary>
    /// Applies the profile-specific configuration to the <see cref="IdentityServerOptions"/> .
    /// This method is called during options post-configuration when the profile is enabled.
    /// Returns a result indicating which options passed and which failed the profile requirements.
    /// </summary>
    /// <param name="options">The <see cref="IdentityServerOptions"/>  to configure.</param>
    /// <returns>A result containing information about which options passed and failed.</returns>
    ProfileValidationResult ApplyProfile(IdentityServerOptions options);

    /// <summary>
    /// Allows the profile to perform client-specific configuration validation.
    /// This is invoked as part of client configuration validation after the built-in validations have run.
    /// Returns a result indicating which client properties passed and which failed the profile requirements.
    /// </summary>
    /// <param name="options">The current IdentityServerOptions.</param>
    /// <param name="context">The client validation context.</param>
    /// <returns>A result containing information about which client properties passed and failed.</returns>
    ProfileValidationResult ValidateClient(IdentityServerOptions options, ClientConfigurationValidationContext context);
}
