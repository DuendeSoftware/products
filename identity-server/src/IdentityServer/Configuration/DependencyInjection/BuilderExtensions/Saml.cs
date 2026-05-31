// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Services.Default;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder extension methods for opting in to SAML 2.0 support.
/// </summary>
public static class IdentityServerBuilderExtensionsSaml
{
    /// <summary>
    /// Adds SAML 2.0 support to IdentityServer.
    /// </summary>
    /// <remarks>
    /// Registers all SAML services and endpoints, and enables the SAML endpoints
    /// in <see cref="EndpointsOptions"/>.
    /// </remarks>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSaml(this IIdentityServerBuilder builder) =>
        builder.AddSaml(_ => { });

    /// <summary>
    /// Adds SAML 2.0 support to IdentityServer with custom endpoint configuration.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configureSaml">Action to configure SAML options. Endpoint paths configured here
    /// are used for routing registration.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSaml(this IIdentityServerBuilder builder, Action<SamlOptions> configureSaml)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureSaml);

        // The configure action runs twice: once here to snapshot endpoint paths
        // for routing registration, and again in the options pipeline to apply
        // the full configuration. It must be idempotent/side-effect free.
        var endpointSnapshot = new SamlOptions();
        configureSaml(endpointSnapshot);

        builder.Services.Configure<IdentityServerOptions>(options =>
        {
            configureSaml(options.Saml);
            options.Endpoints.EnableSamlMetadataEndpoint = true;
            options.Endpoints.EnableSamlSigninEndpoint = true;
            options.Endpoints.EnableSamlSigninCallbackEndpoint = true;
            options.Endpoints.EnableSamlLogoutEndpoint = true;
            options.Endpoints.EnableSamlLogoutCallbackEndpoint = true;
        });

        builder.AddSamlServices(endpointSnapshot.Endpoints);

        return builder;
    }

    /// <summary>
    /// Adds SAML 2.0 protocol services.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="endpoints">The endpoint path configuration.</param>
    /// <returns></returns>
    private static IIdentityServerBuilder AddSamlServices(this IIdentityServerBuilder builder, SamlEndpointOptions endpoints)
    {
        // Replace the no-op registered by AddCoreServices with the new implementation.
        builder.Services.Replace(ServiceDescriptor.Scoped<ISamlLogoutNotificationService, Saml2LogoutNotificationService>());

        // New SAML2 Dependencies

        builder.Services.TryAddSingleton<ISamlSigninStateStore, InMemorySamlSigninStateStore>();
        builder.AddEndpoint<SingleSignOnServiceEndpoint>(EndpointNames.SamlSingleSignOnService, endpoints.SingleSignOnServicePath.EnsureLeadingSlash());
        builder.AddEndpoint<SingleSignOnCallbackEndpoint>(EndpointNames.SamlSingleSignOnCallback, endpoints.SingleSignOnCallbackPath.EnsureLeadingSlash());
        builder.AddEndpoint<SingleLogoutServiceEndpoint>(EndpointNames.SamlSingleLogoutService, endpoints.SingleLogoutServicePath.EnsureLeadingSlash());
        builder.AddEndpoint<SingleLogoutCallbackEndpoint>(EndpointNames.SamlSingleLogoutCallback, endpoints.SingleLogoutCallbackPath.EnsureLeadingSlash());
        builder.AddEndpoint<MetadataEndpoint>(EndpointNames.SamlMetadata, SamlConstants.Defaults.Saml2Path, EndpointHelpers.SamlMetadataHelpers.IsMatch);

        // Use singletons for stateless light weight services
        builder.Services.TryAddEnumerable(new ServiceDescriptor(typeof(IFrontChannelBinding), typeof(HttpPostBinding), ServiceLifetime.Singleton));
        builder.Services.TryAddEnumerable(new ServiceDescriptor(typeof(IFrontChannelBinding), typeof(HttpRedirectBinding), ServiceLifetime.Singleton));

        // Use transient for services that have dependencies that might have any lifespan
        builder.Services.TryAddTransient<IAuthnRequestValidator, AuthnRequestValidator>();
        builder.Services.TryAddTransient<ISamlResourceResolver, DefaultSamlResourceResolver>();
        builder.Services.TryAddTransient<ILogoutRequestValidator, LogoutRequestValidator>();
        builder.Services.TryAddTransient<ISaml2FrontChannelLogoutRequestBuilder, Saml2FrontChannelLogoutRequestBuilder>();
        builder.Services.TryAddTransient<ISaml2SsoInteractionResponseGenerator, Saml2SsoInteractionResponseGenerator>();
        builder.Services.TryAddTransient<ISaml2SsoResponseGenerator, Saml2SSoResponseGenerator>();
        builder.Services.TryAddTransient<ISamlNameIdGenerator, DefaultSamlNameIdGenerator>();
        builder.Services.TryAddTransient<IIdpInitiatedSsoService, DefaultIdpInitiatedSsoService>();
        builder.Services.TryAddTransient<ISaml2MetadataResponseGenerator, Saml2MetadataResponseGenerator>();
        builder.Services.AddTransient<IReturnUrlParser, SamlReturnUrlParser>();

        // Http Writers
        builder.AddHttpWriter<Saml2FrontChannelResult, Saml2FrontChannelResultHttpWriter>();
        builder.AddHttpWriter<Saml2LoginPageResult, Saml2LoginPageResultHttpWriter>();
        builder.AddHttpWriter<Saml2LoginRedirectResult, Saml2LoginRedirectResultHttpWriter>();
        builder.AddHttpWriter<Saml2MetadataResult, Saml2MetadataResultWriter>();
        builder.AddHttpWriter<Saml2LogoutPageResult, Saml2LogoutPageResultHttpWriter>();

        // The reader has state and must be transient.
        builder.Services.TryAddTransient<ServiceProviderEntityResolver>();
        builder.Services.TryAddTransient<ISamlXmlReader>(sp =>
        {
            var resolver = sp.GetRequiredService<ServiceProviderEntityResolver>();
            return new SamlXmlReader
            {
                EntityResolver = resolver.ResolveAsync
            };
        });

        // SAML service provider configuration validator
        builder.Services.TryAddTransient<ISamlServiceProviderConfigurationValidator, DefaultSamlServiceProviderConfigurationValidator>();

        return builder;
    }

    /// <summary>
    /// Adds a custom SAML service provider store.
    /// The store is wrapped in a <see cref="ValidatingSamlServiceProviderStore{T}"/> that validates
    /// service provider configuration on load using the registered <see cref="ISamlServiceProviderConfigurationValidator"/>.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="ISamlServiceProviderStore"/> implementation.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSamlServiceProviderStore<T>(this IIdentityServerBuilder builder)
        where T : class, ISamlServiceProviderStore
    {
        builder.Services.TryAddTransient<ISamlServiceProviderConfigurationValidator, DefaultSamlServiceProviderConfigurationValidator>();
        builder.Services.TryAddTransient<T>();
        builder.Services.AddTransient<ISamlServiceProviderStore, ValidatingSamlServiceProviderStore<T>>();
        return builder;
    }

    /// <summary>
    /// Registers a caching decorator around a custom <see cref="ISamlServiceProviderStore"/> implementation.
    /// The decorator maintains an in-memory cache of <c>SamlServiceProvider</c> configuration objects to reduce
    /// repeated store lookups. Cache duration is configurable via <see cref="IdentityServerOptions.Caching"/>.
    /// </summary>
    /// <typeparam name="T">The type of the concrete service provider store class that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSamlServiceProviderStoreCache<T>(this IIdentityServerBuilder builder)
        where T : class, ISamlServiceProviderStore
    {
        builder.EnsureConfigurationStoreHybridCache();
        builder.Services.TryAddTransient<ISamlServiceProviderConfigurationValidator, DefaultSamlServiceProviderConfigurationValidator>();
        builder.Services.TryAddTransient<T>();
        builder.Services.AddTransient<ValidatingSamlServiceProviderStore<T>>();
        builder.Services.AddTransient<ISamlServiceProviderStore, CachingSamlServiceProviderStore<ValidatingSamlServiceProviderStore<T>>>();
        return builder;
    }
}
