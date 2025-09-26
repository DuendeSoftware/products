using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public static class GoogleExtensions
{
    public static IServiceCollection AddGoogleDynamicProvider(this IServiceCollection services)
    {
        services.Configure<IdentityServerOptions>(options =>
        {
            options.DynamicProviders
                .AddProviderType<GoogleHandler, GoogleOptions, IdentityProvider>(GoogleProviderConfigurationModel.Type);
        });

        services.ConfigureOptions<GoogleDynamicConfigureOptions>();
        services.ConfigureOptions<OAuthPostConfigureOptions<GoogleOptions, GoogleHandler>>();

        services.AddTransient<IProviderConfigurationModelFactory, GoogleProviderConfigurationModelFactory>();

        return services;
    }
}

class GoogleDynamicConfigureOptions
    : ConfigureAuthenticationOptions<GoogleOptions, IdentityProvider>
{
    public GoogleDynamicConfigureOptions(IHttpContextAccessor httpContextAccessor,
        ILogger<GoogleDynamicConfigureOptions> logger) : base(httpContextAccessor, logger)
    {
    }

    protected override void Configure(
        ConfigureAuthenticationContext<GoogleOptions, IdentityProvider> context)
    {
        var googleProvider = context.IdentityProvider;
        var googleOptions = context.AuthenticationOptions;

        googleOptions.ClientId = googleProvider.Properties["ClientId"];
        googleOptions.ClientSecret = googleProvider.Properties["ClientSecret"];
        googleOptions.ClaimActions.MapAll();

        googleOptions.SignInScheme = context.DynamicProviderOptions.SignInScheme;
        googleOptions.CallbackPath = context.PathPrefix + "/signin-oidc";
    }
}

public class GoogleProviderConfigurationModelFactory : IProviderConfigurationModelFactory
{
    public ProviderConfigurationInfo GetProviderConfigurationInfo() => new()
    {
        Type = GoogleProviderConfigurationModel.Type,
        Name = GoogleProviderConfigurationModel.Name
    };

    public bool SupportsType(string type) => type == GoogleProviderConfigurationModel.Type;

    public IProviderConfigurationModel Create() => new GoogleProviderConfigurationModel();

    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider)
    {
        var model = new GoogleProviderConfigurationModel();

        model.IconUrl = identityProvider.Properties["IconUrl"];
        model.ClientId = identityProvider.Properties["ClientId"];
        model.ClientSecret = identityProvider.Properties["ClientSecret"];

        return model;
    }

    public void UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var model = (GoogleProviderConfigurationModel)modelConfiguration;

        identityProviderModel.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        identityProviderModel.Properties["ClientId"] = model.ClientId.Trim();
        identityProviderModel.Properties["ClientSecret"] = model.ClientSecret.Trim();
    }
}

public class GoogleProviderConfigurationModel : IProviderConfigurationModel
{
    public const string Type = "google";
    public const string Name = "Google";

    public string ToFriendlyType() => Name;

    public bool IsIconUrlEditable() => false;

    public string? IconUrl { get; set; } = "";

    [Required]
    [Display(Name = "Client ID")]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client Secret")]
    public string ClientSecret { get; set; } = string.Empty;
}
