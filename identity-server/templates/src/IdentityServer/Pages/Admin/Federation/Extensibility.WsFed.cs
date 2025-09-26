using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.WsFederation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public static class WsFederationExtensions
{
    public static IServiceCollection AddWsFederationDynamicProvider(this IServiceCollection services)
    {
        services.Configure<IdentityServerOptions>(options =>
        {
            options.DynamicProviders
                .AddProviderType<WsFederationHandler, WsFederationOptions, IdentityProvider>(WsFederationProviderConfigurationModel.Type);
        });

        services.ConfigureOptions<WsFederationDynamicConfigureOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<WsFederationOptions>, WsFederationPostConfigureOptions>());

        services.AddTransient<IProviderConfigurationModelFactory, WsFederationProviderConfigurationModelFactory>();

        return services;
    }
}

class WsFederationDynamicConfigureOptions
    : ConfigureAuthenticationOptions<WsFederationOptions, IdentityProvider>
{
    public WsFederationDynamicConfigureOptions(IHttpContextAccessor httpContextAccessor,
        ILogger<WsFederationDynamicConfigureOptions> logger) : base(httpContextAccessor, logger)
    {
    }

    protected override void Configure(
        ConfigureAuthenticationContext<WsFederationOptions, IdentityProvider> context)
    {
        var provider = context.IdentityProvider;
        var options = context.AuthenticationOptions;

        options.MetadataAddress = provider.Properties["MetadataAddress"];
        options.Wtrealm = provider.Properties["Realm"];

        options.SignInScheme = context.DynamicProviderOptions.SignInScheme;
        options.CallbackPath = context.PathPrefix + "/signin-oidc";
    }
}

public class WsFederationProviderConfigurationModelFactory : IProviderConfigurationModelFactory
{
    public ProviderConfigurationInfo GetProviderConfigurationInfo() => new()
    {
        Type = WsFederationProviderConfigurationModel.Type,
        Name = WsFederationProviderConfigurationModel.Name
    };

    public bool SupportsType(string type) => type == WsFederationProviderConfigurationModel.Type;

    public IProviderConfigurationModel Create() => new WsFederationProviderConfigurationModel();

    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider)
    {
        var model = new WsFederationProviderConfigurationModel();

        model.IconUrl = identityProvider.Properties["IconUrl"];
        model.MetadataAddress = identityProvider.Properties["MetadataAddress"];
        model.Realm = identityProvider.Properties["Realm"];

        return model;
    }

    public void UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var model = (WsFederationProviderConfigurationModel)modelConfiguration;

        identityProviderModel.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        identityProviderModel.Properties["MetadataAddress"] = model.MetadataAddress.Trim();
        identityProviderModel.Properties["Realm"] = model.Realm.Trim();
    }
}

public class WsFederationProviderConfigurationModel : IProviderConfigurationModel
{
    public const string Type = "WsFederation";
    public const string Name = "WsFederation";

    public string ToFriendlyType() => Name;

    public bool IsIconUrlEditable() => true;

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
    [Display(Name = "Realm")]
    public string Realm { get; set; } = string.Empty;
}
