// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder extension methods for registering additional services
/// </summary>
public static class IdentityServerBuilderExtensionsAdditional
{
    /// <summary>
    /// Registers a custom <see cref="IExtensionGrantValidator"/> implementation that handles a custom
    /// OAuth 2.0 extension grant type at the token endpoint.
    /// </summary>
    /// <typeparam name="T">The <see cref="IExtensionGrantValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddExtensionGrantValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IExtensionGrantValidator
    {
        builder.Services.AddTransient<IExtensionGrantValidator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IRedirectUriValidator"/> implementation that controls which redirect
    /// URIs are permitted during authorization and end-session requests.
    /// </summary>
    /// <typeparam name="T">The <see cref="IRedirectUriValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddRedirectUriValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IRedirectUriValidator
    {
        builder.Services.AddTransient<IRedirectUriValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds an "AppAuth" (OAuth 2.0 for Native Apps) compliant redirect URI validator (does strict validation but also allows http://127.0.0.1 with random port)
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddAppAuthRedirectUriValidator(this IIdentityServerBuilder builder) => builder.AddRedirectUriValidator<StrictRedirectUriValidatorAppAuth>();

    /// <summary>
    /// Registers a custom <see cref="IResourceOwnerPasswordValidator"/> implementation for validating
    /// user credentials submitted via the Resource Owner Password Credentials grant type.
    /// </summary>
    /// <typeparam name="T">The <see cref="IResourceOwnerPasswordValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddResourceOwnerValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IResourceOwnerPasswordValidator
    {
        builder.Services.AddTransient<IResourceOwnerPasswordValidator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IProfileService"/> implementation that determines which claims
    /// are included in tokens and the userinfo endpoint response for a given user.
    /// The default implementation relies on the authentication cookie as the only source of claims.
    /// </summary>
    /// <typeparam name="T">The <see cref="IProfileService"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the profile service to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddProfileService<T>(this IIdentityServerBuilder builder)
        where T : class, IProfileService
    {
        builder.Services.AddTransient<IProfileService, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IResourceValidator"/> implementation that validates whether
    /// the requested scopes and resources are valid for a given client.
    /// </summary>
    /// <typeparam name="T">The <see cref="IResourceValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddResourceValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IResourceValidator
    {
        builder.Services.AddTransient<IResourceValidator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IScopeParser"/> implementation that parses the raw scope string
    /// from authorization and token requests into individual parsed scope values.
    /// </summary>
    /// <typeparam name="T">The <see cref="IScopeParser"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the scope parser to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddScopeParser<T>(this IIdentityServerBuilder builder)
        where T : class, IScopeParser
    {
        builder.Services.AddTransient<IScopeParser, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IClientStore"/> implementation for loading client configuration.
    /// The store is wrapped in a <see cref="ValidatingClientStore{T}"/> that validates client configuration
    /// on load using the registered <see cref="IClientConfigurationValidator"/>.
    /// </summary>
    /// <typeparam name="T">The <see cref="IClientStore"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the client store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddClientStore<T>(this IIdentityServerBuilder builder)
        where T : class, IClientStore
    {
        builder.Services.TryAddTransient<T>();
        builder.Services.AddTransient<IClientStore, ValidatingClientStore<T>>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IResourceStore"/> implementation for loading identity resources,
    /// API resources, and API scopes used during request validation and token issuance.
    /// </summary>
    /// <typeparam name="T">The <see cref="IResourceStore"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the resource store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddResourceStore<T>(this IIdentityServerBuilder builder)
        where T : class, IResourceStore
    {
        builder.Services.AddTransient<IResourceStore, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IDeviceFlowStore"/> implementation for persisting device flow
    /// authorization codes and user codes during the OAuth 2.0 Device Authorization Grant flow.
    /// </summary>
    /// <typeparam name="T">The <see cref="IDeviceFlowStore"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the device flow store to.</param>
    public static IIdentityServerBuilder AddDeviceFlowStore<T>(this IIdentityServerBuilder builder)
        where T : class, IDeviceFlowStore
    {
        builder.Services.AddTransient<IDeviceFlowStore, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IPersistedGrantStore"/> implementation for persisting grants
    /// such as authorization codes, refresh tokens, reference tokens, and user consent records.
    /// Replace the default in-memory store with a durable implementation for production use.
    /// </summary>
    /// <typeparam name="T">The type of the concrete grant store that is registered in DI.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the persisted grant store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddPersistedGrantStore<T>(this IIdentityServerBuilder builder)
        where T : class, IPersistedGrantStore
    {
        builder.Services.AddTransient<IPersistedGrantStore, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ISigningKeyStore"/> implementation for persisting automatically
    /// managed signing keys. Replace the default file-system store with a durable implementation
    /// (e.g. database or key vault) for production deployments with multiple server instances.
    /// </summary>
    /// <typeparam name="T">The type of the concrete store that is registered in DI.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the signing key store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSigningKeyStore<T>(this IIdentityServerBuilder builder)
        where T : class, ISigningKeyStore
    {
        builder.Services.AddTransient<ISigningKeyStore, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IPushedAuthorizationRequestStore"/> implementation for persisting
    /// Pushed Authorization Requests (PAR). Replace the default in-memory store with a durable
    /// implementation for production use.
    /// </summary>
    /// <typeparam name="T">The type of the concrete store that is registered in DI.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the pushed authorization request store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddPushedAuthorizationRequestStore<T>(this IIdentityServerBuilder builder)
        where T : class, IPushedAuthorizationRequestStore
    {
        builder.Services.AddTransient<IPushedAuthorizationRequestStore, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ISamlSigninStateStore"/> implementation for persisting SAML
    /// authentication state during the single sign-on flow. Replace the default in-memory store
    /// with a durable implementation for production use.
    /// </summary>
    /// <typeparam name="T">The <see cref="ISamlSigninStateStore"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the SAML signin state store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSamlSigninStateStore<T>(this IIdentityServerBuilder builder)
        where T : class, ISamlSigninStateStore
    {
        builder.Services.AddTransient<ISamlSigninStateStore, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ISamlLogoutSessionStore"/> implementation for persisting SAML
    /// logout session tracking state during the single logout flow. Replace the default in-memory store
    /// with a durable implementation for production use.
    /// </summary>
    /// <typeparam name="T">The <see cref="ISamlLogoutSessionStore"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the SAML logout session store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSamlLogoutSessionStore<T>(this IIdentityServerBuilder builder)
        where T : class, ISamlLogoutSessionStore
    {
        builder.Services.AddTransient<ISamlLogoutSessionStore, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ICorsPolicyService"/> implementation that determines whether
    /// a given origin is allowed to make cross-origin requests to IdentityServer endpoints.
    /// </summary>
    /// <typeparam name="T">The type of the concrete CORS policy service that is registered in DI.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the CORS policy service to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddCorsPolicyService<T>(this IIdentityServerBuilder builder)
        where T : class, ICorsPolicyService
    {
        builder.Services.AddTransient<ICorsPolicyService, T>();
        return builder;
    }

    /// <summary>
    /// Registers a caching decorator around a custom <see cref="ICorsPolicyService"/> implementation.
    /// The decorator maintains an in-memory cache of CORS policy evaluation results to reduce repeated
    /// store lookups. Cache duration is configurable via <see cref="IdentityServerOptions.Caching"/>.
    /// </summary>
    /// <typeparam name="T">The type of the concrete CORS policy service that is registered in DI.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the caching CORS policy service to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddCorsPolicyCache<T>(this IIdentityServerBuilder builder)
        where T : class, ICorsPolicyService
    {
        builder.EnsureConfigurationStoreHybridCache();
        builder.Services.TryAddTransient<T>();
        builder.Services.AddTransient<ICorsPolicyService, CachingCorsPolicyService<T>>();
        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ISecretParser"/> implementation for extracting client or API
    /// resource credentials from incoming HTTP requests (e.g. from headers, query strings, or the request body).
    /// Multiple parsers can be registered and are tried in order.
    /// </summary>
    /// <typeparam name="T">The <see cref="ISecretParser"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the secret parser to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSecretParser<T>(this IIdentityServerBuilder builder)
        where T : class, ISecretParser
    {
        builder.Services.AddTransient<ISecretParser, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ISecretValidator"/> implementation for validating parsed client
    /// or API resource credentials against a credential store. Multiple validators can be registered
    /// and are tried in order.
    /// </summary>
    /// <typeparam name="T">The <see cref="ISecretValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the secret validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSecretValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ISecretValidator
    {
        builder.Services.AddTransient<ISecretValidator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a caching decorator around a custom <see cref="IClientStore"/> implementation.
    /// The decorator maintains an in-memory cache of <c>Client</c> configuration objects to reduce
    /// repeated store lookups. Cache duration is configurable via <see cref="IdentityServerOptions.Caching"/>.
    /// </summary>
    /// <typeparam name="T">The type of the concrete client store class that is registered in DI.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the caching client store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddClientStoreCache<T>(this IIdentityServerBuilder builder)
        where T : IClientStore
    {
        builder.EnsureConfigurationStoreHybridCache();
        builder.Services.TryAddTransient(typeof(T));
        builder.Services.AddTransient<ValidatingClientStore<T>>();
        builder.Services.AddTransient<IClientStore, CachingClientStore<ValidatingClientStore<T>>>();

        return builder;
    }

    /// <summary>
    /// Registers a caching decorator around a custom <see cref="IResourceStore"/> implementation.
    /// The decorator maintains an in-memory cache of identity resources, API resources, and API scopes
    /// to reduce repeated store lookups. Cache duration is configurable via <see cref="IdentityServerOptions.Caching"/>.
    /// </summary>
    /// <typeparam name="T">The type of the concrete scope store class that is registered in DI.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the caching resource store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddResourceStoreCache<T>(this IIdentityServerBuilder builder)
        where T : IResourceStore
    {
        builder.EnsureConfigurationStoreHybridCache();
        builder.Services.TryAddTransient(typeof(T));
        builder.Services.AddTransient<IResourceStore, CachingResourceStore<T>>();
        return builder;
    }

    /// <summary>
    /// Registers a caching decorator around a custom <see cref="IIdentityProviderStore"/> implementation.
    /// The decorator maintains an in-memory cache of <c>IdentityProvider</c> configuration objects to reduce
    /// repeated store lookups. Cache duration is configurable via <see cref="IdentityServerOptions.Caching"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the caching identity provider store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddIdentityProviderStoreCache<T>(this IIdentityServerBuilder builder)
        where T : IIdentityProviderStore
    {
        builder.EnsureConfigurationStoreHybridCache();
        builder.Services.TryAddSingleton<IdentityProviderOptionsMonitorCache>();
        builder.Services.TryAddTransient(typeof(T));
        builder.Services.AddTransient<ValidatingIdentityProviderStore<T>>();
        builder.Services.AddTransient<IIdentityProviderStore, CachingIdentityProviderStore<ValidatingIdentityProviderStore<T>>>();

        return builder;
    }



    /// <summary>
    /// Registers a custom <see cref="IAuthorizeInteractionResponseGenerator"/> implementation that
    /// controls the logic at the authorization endpoint for determining when a user must be shown
    /// a UI page (e.g. login, consent, error, or a custom page).
    /// Consider deriving from <c>AuthorizeInteractionResponseGenerator</c> to augment the default behavior.
    /// </summary>
    /// <typeparam name="T">The <see cref="IAuthorizeInteractionResponseGenerator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the response generator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddAuthorizeInteractionResponseGenerator<T>(this IIdentityServerBuilder builder)
        where T : class, IAuthorizeInteractionResponseGenerator
    {
        builder.Services.AddTransient<IAuthorizeInteractionResponseGenerator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ICustomAuthorizeRequestValidator"/> implementation for adding
    /// additional validation logic to authorization endpoint requests, such as enforcing custom
    /// parameter requirements or business rules.
    /// </summary>
    /// <typeparam name="T">The <see cref="ICustomAuthorizeRequestValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddCustomAuthorizeRequestValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ICustomAuthorizeRequestValidator
    {
        builder.Services.AddTransient<ICustomAuthorizeRequestValidator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ICustomTokenRequestValidator"/> implementation for adding
    /// additional validation logic to token endpoint requests, such as enforcing custom parameter
    /// requirements or enriching the token request context.
    /// </summary>
    /// <typeparam name="T">The <see cref="ICustomTokenRequestValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddCustomTokenRequestValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ICustomTokenRequestValidator
    {
        builder.Services.AddTransient<ICustomTokenRequestValidator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="ICustomBackchannelAuthenticationValidator"/> implementation for
    /// adding additional validation logic to CIBA (Client-Initiated Backchannel Authentication) requests.
    /// </summary>
    /// <typeparam name="T">The <see cref="ICustomBackchannelAuthenticationValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddCustomBackchannelAuthenticationRequestValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ICustomBackchannelAuthenticationValidator
    {
        builder.Services.AddTransient<ICustomBackchannelAuthenticationValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds support for client authentication using JWT bearer assertions (private_key_jwt).
    /// Registers the <c>JwtBearerClientAssertionSecretParser</c> and <c>PrivateKeyJwtSecretValidator</c>
    /// so that clients can authenticate at the token endpoint using a signed JWT instead of a shared secret.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add JWT bearer client authentication to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddJwtBearerClientAuthentication(this IIdentityServerBuilder builder)
    {
        builder.AddSecretParser<JwtBearerClientAssertionSecretParser>();
        builder.AddSecretValidator<PrivateKeyJwtSecretValidator>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IClientConfigurationValidator"/> implementation that validates
    /// client configuration when clients are loaded from the store, allowing enforcement of
    /// organization-specific client configuration rules.
    /// </summary>
    /// <typeparam name="T">The <see cref="IClientConfigurationValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddClientConfigurationValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IClientConfigurationValidator
    {
        builder.Services.AddTransient<IClientConfigurationValidator, T>();

        return builder;
    }


    /// <summary>
    /// Registers a custom <see cref="IIdentityProviderConfigurationValidator"/> implementation that
    /// validates dynamic identity provider configuration when providers are loaded from the store.
    /// </summary>
    /// <typeparam name="T">The <see cref="IIdentityProviderConfigurationValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddIdentityProviderConfigurationValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IIdentityProviderConfigurationValidator
    {
        builder.Services.AddTransient<IIdentityProviderConfigurationValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the X.509 secret parsers and validators required for mutual TLS (mTLS) client authentication.
    /// Registers <c>MutualTlsSecretParser</c>, <c>X509ThumbprintSecretValidator</c>, and
    /// <c>X509NameSecretValidator</c> so that clients can authenticate using their TLS client certificate.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add mTLS secret validators to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddMutualTlsSecretValidators(this IIdentityServerBuilder builder)
    {
        builder.AddSecretParser<MutualTlsSecretParser>();
        builder.AddSecretValidator<X509ThumbprintSecretValidator>();
        builder.AddSecretValidator<X509NameSecretValidator>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IBackChannelLogoutService"/> implementation that handles
    /// sending back-channel logout notifications to clients when a user's session ends.
    /// </summary>
    /// <typeparam name="T">The <see cref="IBackChannelLogoutService"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the back-channel logout service to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddBackChannelLogoutService<T>(this IIdentityServerBuilder builder)
        where T : class, IBackChannelLogoutService
    {
        builder.Services.AddTransient<IBackChannelLogoutService, T>();

        return builder;
    }

    /// <summary>
    /// Configures the named <see cref="HttpClient"/> used for sending back-channel logout notifications
    /// to client applications. Use this to customize timeouts, add delegating handlers, or configure
    /// other <see cref="HttpClient"/> settings for logout HTTP calls.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to configure the HTTP client on.</param>
    /// <param name="configureClient">An optional delegate to configure the <see cref="HttpClient"/> instance.
    /// If not provided, a default timeout of <see cref="IdentityServerConstants.HttpClients.DefaultTimeoutSeconds"/> is applied.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> for further HTTP client configuration (e.g. adding handlers).</returns>
    public static IHttpClientBuilder AddBackChannelLogoutHttpClient(this IIdentityServerBuilder builder, Action<HttpClient>? configureClient = null)
    {
        const string name = IdentityServerConstants.HttpClients.BackChannelLogoutHttpClient;
        IHttpClientBuilder httpBuilder;

        if (configureClient != null)
        {
            httpBuilder = builder.Services.AddHttpClient(name, configureClient);
        }
        else
        {
            httpBuilder = builder.Services.AddHttpClient(name)
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(IdentityServerConstants.HttpClients.DefaultTimeoutSeconds);
                });
        }

        builder.Services.AddTransient<IBackChannelLogoutHttpClient>(s =>
        {
            var httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(name);
            var loggerFactory = s.GetRequiredService<ILoggerFactory>();

            return new DefaultBackChannelLogoutHttpClient(httpClient, loggerFactory);
        });

        return httpBuilder;
    }

    /// <summary>
    /// Configures the named <see cref="HttpClient"/> used for fetching JWT request objects from a
    /// <c>request_uri</c> parameter at the authorization endpoint. Use this to customize timeouts,
    /// add delegating handlers, or configure other <see cref="HttpClient"/> settings for request URI fetches.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to configure the HTTP client on.</param>
    /// <param name="configureClient">An optional delegate to configure the <see cref="HttpClient"/> instance.
    /// If not provided, a default timeout of <see cref="IdentityServerConstants.HttpClients.DefaultTimeoutSeconds"/> is applied.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> for further HTTP client configuration (e.g. adding handlers).</returns>
    public static IHttpClientBuilder AddJwtRequestUriHttpClient(this IIdentityServerBuilder builder, Action<HttpClient>? configureClient = null)
    {
        const string name = IdentityServerConstants.HttpClients.JwtRequestUriHttpClient;
        IHttpClientBuilder httpBuilder;

        if (configureClient != null)
        {
            httpBuilder = builder.Services.AddHttpClient(name, configureClient);
        }
        else
        {
            httpBuilder = builder.Services.AddHttpClient(name)
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(IdentityServerConstants.HttpClients.DefaultTimeoutSeconds);
                });
        }

        builder.Services.AddTransient<IJwtRequestUriHttpClient, DefaultJwtRequestUriHttpClient>(s =>
        {
            var httpClientFactory = s.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(name);
            var loggerFactory = s.GetRequiredService<ILoggerFactory>();
            var options = s.GetRequiredService<IdentityServerOptions>();

            return new DefaultJwtRequestUriHttpClient(httpClient, options, loggerFactory);
        });

        return httpBuilder;
    }

    /// <summary>
    /// Registers a custom <see cref="IUserSession"/> implementation that manages the user's authentication
    /// session, including reading and writing the session cookie and tracking session identifiers.
    /// The service is registered as scoped.
    /// </summary>
    /// <typeparam name="T">The <see cref="IUserSession"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the user session to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddUserSession<T>(this IIdentityServerBuilder builder)
        where T : class, IUserSession
    {
        // This is added as scoped due to the note regarding the AuthenticateAsync
        // method in the Duende.Services.DefaultUserSession implementation.
        builder.Services.AddScoped<IUserSession, T>();

        return builder;
    }


    /// <summary>
    /// Registers a custom <see cref="IIdentityProviderStore"/> implementation for loading dynamic
    /// external identity provider configuration used by the dynamic providers feature.
    /// The store is wrapped in a <see cref="ValidatingIdentityProviderStore{T}"/> that validates
    /// provider configuration on load.
    /// </summary>
    /// <typeparam name="T">The <see cref="IIdentityProviderStore"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the identity provider store to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddIdentityProviderStore<T>(this IIdentityServerBuilder builder)
        where T : class, IIdentityProviderStore
    {
        builder.Services.TryAddSingleton<IdentityProviderOptionsMonitorCache>();
        builder.Services.TryAddTransient<T>();
        builder.Services.TryAddTransient<ValidatingIdentityProviderStore<T>>();
        builder.Services.AddTransient<IIdentityProviderStore, NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<T>>>();

        return builder;
    }


    /// <summary>
    /// Registers a custom <see cref="IBackchannelAuthenticationUserValidator"/> implementation that
    /// validates the user hint provided in a CIBA (Client-Initiated Backchannel Authentication) request,
    /// resolving the hint to a subject identifier.
    /// </summary>
    /// <typeparam name="T">The <see cref="IBackchannelAuthenticationUserValidator"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validator to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddBackchannelAuthenticationUserValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IBackchannelAuthenticationUserValidator
    {
        builder.Services.AddTransient<IBackchannelAuthenticationUserValidator, T>();

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IBackchannelAuthenticationUserNotificationService"/> implementation
    /// that is responsible for notifying the end user of a pending CIBA authentication request
    /// (e.g. by sending a push notification or SMS).
    /// </summary>
    /// <typeparam name="T">The <see cref="IBackchannelAuthenticationUserNotificationService"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the notification service to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddBackchannelAuthenticationUserNotificationService<T>(this IIdentityServerBuilder builder)
        where T : class, IBackchannelAuthenticationUserNotificationService
    {
        builder.Services.AddTransient<IBackchannelAuthenticationUserNotificationService, T>();

        return builder;
    }

    /// <summary>
    /// Ensures a keyed <see cref="HybridCache"/> for the configuration store cache is registered
    /// exactly once. Subsequent calls are no-ops, so apps that register their own keyed
    /// <see cref="HybridCache"/> first will not have it overridden.
    /// </summary>
    internal static void EnsureConfigurationStoreHybridCache(this IIdentityServerBuilder builder)
    {
        if (builder.Services.Any(d =>
                d.ServiceType == typeof(HybridCache) &&
                d.IsKeyedService &&
                ServiceProviderKeys.ConfigurationStoreCache.Equals(d.ServiceKey)))
        {
            return;
        }

        builder.Services.AddKeyedHybridCache(ServiceProviderKeys.ConfigurationStoreCache);
    }

}
