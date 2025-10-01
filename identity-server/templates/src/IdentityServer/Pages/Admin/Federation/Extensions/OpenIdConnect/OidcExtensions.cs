namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.OpenIdConnect;

public static class OidcExtensions
{
    public static IFederationGatewayBuilder AddOidcDynamicProvider(this IFederationGatewayBuilder builder)
    {
        // REVIEW: this assumes the IdentityServer configuration alread has the Oidc dynamic provider registered, and only adds management UI functionality

        builder.Services.AddTransient<IProviderConfigurationModelFactory, OidcProviderConfigurationModelFactory>();

        return builder;
    }
}