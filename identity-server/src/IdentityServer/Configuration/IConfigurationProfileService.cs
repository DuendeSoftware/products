// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Configuration;

using Duende.IdentityServer.Validation;

/// <summary>
/// Defines the behavior for applying a specific configuration profile to IdentityServerOptions.
/// Implementations of this interface handle the logic for a particular profile (e.g., FAPI 2.0, OAuth 2.1).
/// </summary>
public interface IConfigurationProfileService
{
    /// <summary>
    /// Gets the name of the profile that this service handles.
    /// This should match the constants in <see cref="Models.ConfigurationProfiles"/>.
    /// </summary>
    string ProfileName { get; }

    /// <summary>
    /// Applies the profile-specific configuration to the IdentityServerOptions.
    /// This method is called during options post-configuration when the profile is enabled.
    /// </summary>
    /// <param name="options">The IdentityServerOptions to configure.</param>
    void ApplyProfile(IdentityServerOptions options);

    /// <summary>
    /// Allows the profile to perform client-specific configuration validation.
    /// This is invoked as part of client configuration validation after the built-in validations have run.
    /// Implementations should call <see cref="ClientConfigurationValidationContext.SetError(string)"/> to report errors.
    /// </summary>
    /// <param name="options">The current IdentityServerOptions.</param>
    /// <param name="context">The client validation context.</param>
    void ValidateClient(IdentityServerOptions options, ClientConfigurationValidationContext context);
}
