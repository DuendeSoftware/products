using System.ComponentModel.DataAnnotations;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.SAML2;

public class Saml2ProviderConfigurationModel : IProviderConfigurationModel
{
    public const string Type = "saml2";
    public const string Name = "SAML 2";

    public string ToFriendlyType() => Name;
    public string GetCallbackUrlSuffix() => "";

    [Display(Name = "Icon URL", Order = 1)]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "Icon URL must start with http or https.")]
    public string? IconUrl { get; set; }

    [Required]
    [Display(Name = "SPEntityId", Order = 2, Description = "An absolute URI that identifies Duende IdentityServer as a service provider.")]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "URL must start with http or https.")]
    public string SPEntityId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "IdpEntityId", Order = 3, Description = "An absolute URI that identifies the SAML 2 Identity Provider.")]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "URL must start with http or https.")]
    public string IdpEntityId { get; set; } = string.Empty;
}
