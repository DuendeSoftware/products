// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// Represents the result of validating properties against a configuration profile.
/// This can be used for validating both IdentityServerOptions  and Client configurations.
/// Contains information about which properties passed and which failed the profile requirements.
/// </summary>
public class ProfileValidationResult
{
    /// <summary>
    /// Gets the list of properties that passed the profile requirements.
    /// </summary>
    public ICollection<ProfileCheck> Passed { get; } = new List<ProfileCheck>();

    /// <summary>
    /// Gets the list of properties that failed the profile requirements.
    /// </summary>
    public ICollection<ProfileCheck> Failed { get; } = new List<ProfileCheck>();

    /// <summary>
    /// Returns true if all properties passed the profile requirements.
    /// </summary>
    public bool IsValid => Failed.Count == 0;

    /// <summary>
    /// Adds a passed property check to the result.
    /// </summary>
    /// <param name="propertyPath">The path to the property (e.g., "PushedAuthorization.Required", "Client.RequirePkce").</param>
    /// <param name="wasOverridden">Whether the profile overrode the user's configuration.</param>
    public void AddPassed(string propertyPath, bool wasOverridden = false) => Passed.Add(new ProfileCheck
    {
        Path = propertyPath,
        WasOverridden = wasOverridden
    });

    /// <summary>
    /// Adds a failed property check to the result.
    /// </summary>
    /// <param name="propertyPath">The path to the property (e.g., "PushedAuthorization.Required", "Client.RequirePkce").</param>
    /// <param name="description">A description of why the property failed.</param>
    public void AddFailed(string propertyPath, string description) => Failed.Add(new ProfileCheck
    {
        Path = propertyPath,
        Description = description
    });
}
