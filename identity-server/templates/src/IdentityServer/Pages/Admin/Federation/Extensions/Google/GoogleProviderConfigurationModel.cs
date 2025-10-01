using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.Google;

public class GoogleProviderConfigurationModel : IProviderConfigurationModel
{
    public const string Type = "google";
    public const string Name = "Google";

    public string ToFriendlyType() => Name;
    public string GetCallbackUrlSuffix() => "signin";

    [HiddenInput]
    public string? IconUrl { get; set; }

    [Required]
    [Display(Name = "Client ID", Order = 1)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client Secret", Order = 2)]
    public string ClientSecret { get; set; } = string.Empty;
}
