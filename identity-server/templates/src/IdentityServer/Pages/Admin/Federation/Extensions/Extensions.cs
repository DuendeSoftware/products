using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions;

public static class Extensions
{
    public static IIdentityServerBuilder AddFederationGateway(this IIdentityServerBuilder builder, Action<IFederationGatewayBuilder>? federationGatewayBuilderAction = null)
    {
        // Callback
        var federationGatewayBuilder = new FederationGatewayBuilder(builder.Services);
        federationGatewayBuilderAction?.Invoke(federationGatewayBuilder);

        // Stores
        builder.AddFederationIdentityProviderStore();

        return builder;
    }

    public static IIdentityServerBuilder AddFederationIdentityProviderStore(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddTransient<FederationIdentityProviderStore>();
        builder.Services.TryAddTransient<ValidatingIdentityProviderStore<FederationIdentityProviderStore>>();
        builder.Services.AddTransient<IIdentityProviderStore, NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<FederationIdentityProviderStore>>>();

        return builder;
    }
}