namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions;

public interface IProviderConfigurationModel
{
    string ToFriendlyType();

    string GetCallbackUrlSuffix();

    string? IconUrl { get; set; }
}
