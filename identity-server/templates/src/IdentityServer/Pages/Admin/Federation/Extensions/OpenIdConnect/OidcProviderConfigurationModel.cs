using System.ComponentModel.DataAnnotations;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.OpenIdConnect;

public class OidcProviderConfigurationModel : IProviderConfigurationModel
{
    public const string Type = "oidc";
    public const string Name = "OpenID Connect";

    public string ToFriendlyType() => Name;

    public string GetCallbackUrlSuffix() => "signin-oidc";

    [Display(Name = "Icon URL", Order = 1)]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "Icon URL must start with http or https.")]
    public string? IconUrl { get; set; }

    [Required]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "Authority URL must start with http or https.")]
    [Display(Name = "Authority", Prompt = "https://auth.example.com/", Order = 2)]
    public string Authority { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client ID", Order = 3)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client Secret", Order = 4)]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Use PKCE", Order = 5)]
    public bool UsePkce { get; set; }

    [Required]
    [Display(Name = "Response type", Order = 6)]
    public string ResponseType { get; set; } = "code";

    [Required]
    [Display(Name = "Scope", Order = 7)]
    public string Scope { get; set; } = "openid";
}
