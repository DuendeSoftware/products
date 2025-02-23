// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Configuration.Configuration;

/// <summary>
/// Options for dynamic client registration.
/// </summary>
public class DynamicClientRegistrationOptions
{
    /// <summary>
    /// Gets or sets the lifetime of secrets generated for clients. If unset,
    /// generated secrets will have no expiration. Defaults to null (secrets
    /// never expire).
    /// </summary>
    public TimeSpan? SecretLifetime { get; set; }
}
