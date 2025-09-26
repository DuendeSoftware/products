using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public class OidcProviderConfigurationModelFactory : IProviderConfigurationModelFactory
{
    public ProviderConfigurationInfo GetProviderConfigurationInfo() => new()
    {
        Type = OidcProviderConfigurationModel.Type,
        Name = OidcProviderConfigurationModel.Name
    };

    public bool SupportsType(string type) => type == OidcProviderConfigurationModel.Type;

    public IProviderConfigurationModel Create() => new OidcProviderConfigurationModel();

    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider)
    {
        var model = new OidcProviderConfigurationModel();

        model.IconUrl = identityProvider.Properties["IconUrl"];
        model.Authority = identityProvider.Properties["Authority"];
        model.ClientId = identityProvider.Properties["ClientId"];
        model.ClientSecret = identityProvider.Properties["ClientSecret"];
        model.ResponseType = identityProvider.Properties["ResponseType"];
        model.Scope = identityProvider.Properties["Scope"];
        model.UsePkce = !identityProvider.Properties.ContainsKey("UsePkce") ||
                        "true".Equals(identityProvider.Properties["UsePkce"], StringComparison.Ordinal);

        return model;
    }

    public void UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var model = (OidcProviderConfigurationModel)modelConfiguration;


        identityProviderModel.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        identityProviderModel.Properties["Authority"] = model.Authority.Trim();
        identityProviderModel.Properties["ClientId"] = model.ClientId.Trim();
        identityProviderModel.Properties["ClientSecret"] = model.ClientSecret.Trim();
        identityProviderModel.Properties["ResponseType"] = model.ResponseType.Trim();
        identityProviderModel.Properties["Scope"] = model.Scope.Trim();
        identityProviderModel.Properties["UsePkce"] = model.UsePkce ? "true" : "false";
    }
}

public class OidcProviderConfigurationModel : IProviderConfigurationModel
{
    public const string Type = "oidc";
    public const string Name = "OpenID Connect";

    public string ToFriendlyType() => Name;

    public bool IsIconUrlEditable() => true;

    [Display(Name = "Icon URL")]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "Icon URL must start with http or https.")]
    public string? IconUrl { get; set; }

    [Required]
    [Url]
    [RegularExpression(@"^(http|https)://(.*)", ErrorMessage = "Authority URL must start with http or https.")]
    [Display(Name = "Authority", Prompt = "https://auth.example.com/")]
    public string Authority { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client ID")]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client Secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Use PKCE")]
    public bool UsePkce { get; set; }

    [Required]
    [Display(Name = "Response type")]
    public string ResponseType { get; set; } = "code";

    [Required]
    [Display(Name = "Scope")]
    public string Scope { get; set; } = "openid profile";
}
