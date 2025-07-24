// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Provides configuration options for enabling and managing preview features in IdentityServer.
/// </summary>
public class PreviewFeatureOptions
{
    /// <summary>
    /// Enables Caching of Discovery Document based on ResponseCaching Interval
    /// </summary>
    public bool EnableDiscoveryDocumentCache { get; set; }

    /// <summary>
    /// When clients authenticate with private_key_jwt assertions, validate the audience of the assertion strictly: the audience must be this IdentityServer's issuer identifier as a single string.
    /// </summary>
    public bool StrictClientAssertionAudienceValidation { get; set; }

    /// <summary>
    /// DiscoveryDocument Cache Duration
    /// </summary>
    public TimeSpan DiscoveryDocumentCacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}
