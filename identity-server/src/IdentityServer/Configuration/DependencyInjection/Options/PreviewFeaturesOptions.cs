namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Preview Features Options
/// </summary>
public class PreviewFeaturesOptions
{
    /// <summary>
    /// Enables Caching of Discovery Document based on ResponseCaching Interval 
    /// </summary>
    public bool EnableDiscoveryDocumentCache { get; set; } = false;
    
    /// <summary>
    /// DiscoveryDocument Cache Duration
    /// </summary>
    public TimeSpan DiscoveryDocumentCacheDuration{ get; set; } = TimeSpan.FromMinutes(1);
}