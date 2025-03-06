// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Preview Features Options
/// </summary>
public class PreviewFeaturesOptions
{
    /// <summary>
    /// Enables Caching of Discovery Document based on ResponseCaching Interval.
    /// Important: Requires in memory caching be enabled (services.AddInMemoryCaching())
    /// </summary>
    [Experimental("DUENDEPREVIEW001", UrlFormat = "https://duende.link/previewfeatures?id={0}")]
    public bool EnableDiscoveryDocumentCache { get; set; } = false;

    /// <summary>
    /// DiscoveryDocument Cache Duration
    /// </summary>
    public TimeSpan DiscoveryDocumentCacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}