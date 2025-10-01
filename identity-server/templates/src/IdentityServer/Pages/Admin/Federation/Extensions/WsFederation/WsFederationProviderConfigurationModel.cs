using System.ComponentModel.DataAnnotations;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.WsFederation;

public class WsFederationProviderConfigurationModel : IProviderConfigurationModel
{
    public const string Type = "WsFederation";
    public const string Name = "WsFederation";

    public string ToFriendlyType() => Name;
    public string GetCallbackUrlSuffix() => "signin";

    [Display(Name = "Icon URL")]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "Icon URL must start with http or https.")]
    public string? IconUrl { get; set; }

    [Required]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "Metadata address must start with http or https.")]
    [Display(Name = "Metadata Address", Prompt = "https://<ADFS FQDN or AAD tenant>/FederationMetadata/2007-06/FederationMetadata.xml")]
    public string MetadataAddress { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Relying Party Id")]
    public string RelyingPartyId { get; set; } = string.Empty;

    [Display(Name = "Allow Idp Initiated Login")]
    public bool AllowIdpInitiated { get; set; }
}
