// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.Metadata;
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores;
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
    /// in <see cref="EndpointsOptions"/>. The IdP-initiated SSO endpoint is not
    /// enabled by default; set <see cref="EndpointsOptions.EnableSamlIdpInitiatedEndpoint"/>
    /// to <c>true</c> explicitly if you need it.
    /// </remarks>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSaml(this IIdentityServerBuilder builder)
    {
        builder.AddSamlServices();

        builder.Services.Configure<IdentityServerOptions>(options =>
        {
            options.Endpoints.EnableSamlMetadataEndpoint = true;
            options.Endpoints.EnableSamlSigninEndpoint = true;
            options.Endpoints.EnableSamlSigninCallbackEndpoint = true;
            options.Endpoints.EnableSamlLogoutEndpoint = true;
            options.Endpoints.EnableSamlLogoutCallbackEndpoint = true;
            // EnableSamlIdpInitiatedEndpoint intentionally left false — requires explicit opt-in.
        });

        return builder;
    }

    /// <summary>
    /// Adds SAML 2.0 protocol services.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    private static IIdentityServerBuilder AddSamlServices(this IIdentityServerBuilder builder)
    {
        // SAML 2.0 endpoints
        builder.AddEndpoint<SamlMetaDataEndpoint>(EndpointNames.SamlMetadata, ProtocolRoutePaths.SamlMetadata.EnsureLeadingSlash());
        builder.AddEndpoint<SamlSigninEndpoint>(EndpointNames.SamlSignin, ProtocolRoutePaths.SamlSignin.EnsureLeadingSlash());
        builder.AddEndpoint<SamlSigninCallbackEndpoint>(EndpointNames.SamlSigninCallback, ProtocolRoutePaths.SamlSigninCallback.EnsureLeadingSlash());
        builder.AddEndpoint<SamlIdpInitiatedEndpoint>(EndpointNames.SamlIdpInitiated, ProtocolRoutePaths.SamlIdpInitiated.EnsureLeadingSlash());
        builder.AddEndpoint<SamlSingleLogoutEndpoint>(EndpointNames.SamlLogout, ProtocolRoutePaths.SamlLogout.EnsureLeadingSlash());
        builder.AddEndpoint<SamlSingleLogoutCallbackEndpoint>(EndpointNames.SamlLogoutCallback, ProtocolRoutePaths.SamlLogoutCallback.EnsureLeadingSlash());

        // Serializers (Transient)
        builder.Services.AddTransient<ISamlResultSerializer<SamlErrorResponse>, SamlErrorResponseXmlSerializer>();
        builder.Services.AddTransient<ISamlResultSerializer<SamlResponse>, SamlResponse.Serializer>();
        builder.Services.AddTransient<ISamlResultSerializer<LogoutResponse>, LogoutResponse.Serializer>();

        // HTTP response writers
        builder.AddHttpWriter<SamlErrorResponse, SamlErrorResponse.ResponseWriter>();
        builder.AddHttpWriter<SamlResponse, SamlResponse.ResponseWriter>();
        builder.AddHttpWriter<LogoutResponse, LogoutResponse.ResponseWriter>();

        // Processors (Scoped)
        builder.Services.AddScoped<SamlSigninRequestProcessor>();
        builder.Services.AddScoped<SamlSigninCallbackRequestProcessor>();
        builder.Services.AddScoped<SamlIdpInitiatedRequestProcessor>();
        builder.Services.AddScoped<SamlLogoutRequestProcessor>();
        builder.Services.AddScoped<SamlLogoutCallbackProcessor>();

        // Builders (Scoped)
        builder.Services.AddScoped<SamlResponseBuilder>();
        builder.Services.AddScoped<LogoutResponseBuilder>();
        builder.Services.AddScoped<SamlFrontChannelLogoutRequestBuilder>();

        // Parsers / Extractors (Scoped)
        builder.Services.AddScoped<AuthNRequestParser>();
        builder.Services.AddScoped<LogoutRequestParser>();
        builder.Services.AddScoped<SamlSigninRequestExtractor>();
        builder.Services.AddScoped<SamlLogoutRequestExtractor>();

        // Infrastructure (Scoped)
        builder.Services.AddScoped<SamlUrlBuilder>();
        builder.Services.AddScoped<SamlClaimsService>();
        builder.Services.AddScoped<SamlNameIdGenerator>();
        builder.Services.AddScoped<SamlResponseSigner>();
        builder.Services.AddScoped<SamlProtocolMessageSigner>();
        builder.Services.AddScoped<SamlAssertionEncryptor>();
        builder.Services.AddScoped<SamlRequestValidator>();
        builder.Services.TryAddScoped(typeof(SamlRequestSignatureValidator<,>));

        // Interface → Implementation (TryAddScoped for extensibility)
        builder.Services.TryAddScoped<ISamlSigninInteractionResponseGenerator, DefaultSamlSigninInteractionResponseGenerator>();
        builder.Services.TryAddScoped<ISamlSigningService, SamlSigningService>();
        // Replace the no-op registered by AddCoreServices with the real implementation.
        builder.Services.Replace(ServiceDescriptor.Scoped<ISamlLogoutNotificationService, SamlLogoutNotificationService>());
        builder.Services.TryAddScoped<ISamlInteractionService, DefaultSamlInteractionService>();

        // State management (Singleton)
        builder.Services.TryAddSingleton<SamlSigninStateIdCookie>();
        builder.Services.TryAddSingleton<ISamlSigninStateStore, DistributedCacheSamlSigninStateStore>();

        return builder;
    }

    /// <summary>
    /// Adds a custom SAML service provider store.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="ISamlServiceProviderStore"/> implementation.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSamlServiceProviderStore<T>(this IIdentityServerBuilder builder)
        where T : class, ISamlServiceProviderStore
    {
        builder.Services.AddTransient<ISamlServiceProviderStore, T>();
        return builder;
    }
}
