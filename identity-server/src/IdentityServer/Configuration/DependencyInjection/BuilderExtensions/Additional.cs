// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder extension methods for registering additional services
/// </summary>
public static class IdentityServerBuilderExtensionsAdditional
{
    /// <summary>
    /// Adds the extension grant validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddExtensionGrantValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IExtensionGrantValidator
    {
        builder.Services.AddTransient<IExtensionGrantValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds a redirect URI validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddRedirectUriValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IRedirectUriValidator
    {
        builder.Services.AddTransient<IRedirectUriValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds an "AppAuth" (OAuth 2.0 for Native Apps) compliant redirect URI validator (does strict validation but also allows http://127.0.0.1 with random port)
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddAppAuthRedirectUriValidator(this IIdentityServerBuilder builder) => builder.AddRedirectUriValidator<StrictRedirectUriValidatorAppAuth>();

    /// <summary>
    /// Adds the resource owner validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddResourceOwnerValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IResourceOwnerPasswordValidator
    {
        builder.Services.AddTransient<IResourceOwnerPasswordValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the profile service.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddProfileService<T>(this IIdentityServerBuilder builder)
        where T : class, IProfileService
    {
        builder.Services.AddTransient<IProfileService, T>();

        return builder;
    }

    /// <summary>
    /// Adds a resource validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddResourceValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IResourceValidator
    {
        builder.Services.AddTransient<IResourceValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds a scope parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddScopeParser<T>(this IIdentityServerBuilder builder)
        where T : class, IScopeParser
    {
        builder.Services.AddTransient<IScopeParser, T>();

        return builder;
    }

    /// <summary>
    /// Adds a client store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddClientStore<T>(this IIdentityServerBuilder builder)
        where T : class, IClientStore
    {
        builder.Services.TryAddTransient<T>();
        builder.Services.AddTransient<IClientStore, ValidatingClientStore<T>>();

        return builder;
    }

    /// <summary>
    /// Adds a resource store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddResourceStore<T>(this IIdentityServerBuilder builder)
        where T : class, IResourceStore
    {
        builder.Services.AddTransient<IResourceStore, T>();

        return builder;
    }

    /// <summary>
    /// Adds a device flow store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    public static IIdentityServerBuilder AddDeviceFlowStore<T>(this IIdentityServerBuilder builder)
        where T : class, IDeviceFlowStore
    {
        builder.Services.AddTransient<IDeviceFlowStore, T>();

        return builder;
    }

    /// <summary>
    /// Adds a persisted grant store.
    /// </summary>
    /// <typeparam name="T">The type of the concrete grant store that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static IIdentityServerBuilder AddPersistedGrantStore<T>(this IIdentityServerBuilder builder)
        where T : class, IPersistedGrantStore
    {
        builder.Services.AddTransient<IPersistedGrantStore, T>();

        return builder;
    }

    /// <summary>
    /// Adds a signing key store.
    /// </summary>
    /// <typeparam name="T">The type of the concrete store that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static IIdentityServerBuilder AddSigningKeyStore<T>(this IIdentityServerBuilder builder)
        where T : class, ISigningKeyStore
    {
        builder.Services.AddTransient<ISigningKeyStore, T>();

        return builder;
    }

    /// <summary>
    /// Adds a pushed authorization request store.
    /// </summary>
    /// <typeparam name="T">The type of the concrete store that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static IIdentityServerBuilder AddPushedAuthorizationRequestStore<T>(this IIdentityServerBuilder builder)
        where T : class, IPushedAuthorizationRequestStore
    {
        builder.Services.AddTransient<IPushedAuthorizationRequestStore, T>();

        return builder;
    }

    /// <summary>
    /// Adds a CORS policy service.
    /// </summary>
    /// <typeparam name="T">The type of the concrete CORS policy service that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddCorsPolicyService<T>(this IIdentityServerBuilder builder)
        where T : class, ICorsPolicyService
    {
        builder.Services.AddTransient<ICorsPolicyService, T>();
        return builder;
    }

    /// <summary>
    /// Adds a CORS policy service cache.
    /// </summary>
    /// <typeparam name="T">The type of the concrete CORS policy service that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddCorsPolicyCache<T>(this IIdentityServerBuilder builder)
        where T : class, ICorsPolicyService
    {
        builder.Services.TryAddTransient<T>();
        builder.Services.AddTransient<ICorsPolicyService, CachingCorsPolicyService<T>>();
        return builder;
    }

    /// <summary>
    /// Adds the secret parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSecretParser<T>(this IIdentityServerBuilder builder)
        where T : class, ISecretParser
    {
        builder.Services.AddTransient<ISecretParser, T>();

        return builder;
    }

    /// <summary>
    /// Adds the secret validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddSecretValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ISecretValidator
    {
        builder.Services.AddTransient<ISecretValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the client store cache.
    /// </summary>
    /// <typeparam name="T">The type of the concrete client store class that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddClientStoreCache<T>(this IIdentityServerBuilder builder)
        where T : IClientStore
    {
        builder.Services.TryAddTransient(typeof(T));
        builder.Services.AddTransient<ValidatingClientStore<T>>();
        builder.Services.AddTransient<IClientStore, CachingClientStore<ValidatingClientStore<T>>>();

        return builder;
    }

    /// <summary>
    /// Adds the client store cache.
    /// </summary>
    /// <typeparam name="T">The type of the concrete scope store class that is registered in DI.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddResourceStoreCache<T>(this IIdentityServerBuilder builder)
        where T : IResourceStore
    {
        builder.Services.TryAddTransient(typeof(T));
        builder.Services.AddTransient<IResourceStore, CachingResourceStore<T>>();
        return builder;
    }

    /// <summary>
    /// Adds the identity provider store cache.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityProviderStoreCache<T>(this IIdentityServerBuilder builder)
        where T : IIdentityProviderStore
    {
        builder.Services.TryAddTransient(typeof(T));
        builder.Services.AddTransient<ValidatingIdentityProviderStore<T>>();
        builder.Services.AddTransient<IIdentityProviderStore, CachingIdentityProviderStore<ValidatingIdentityProviderStore<T>>>();

        return builder;
    }



    /// <summary>
    /// Adds the authorize interaction response generator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddAuthorizeInteractionResponseGenerator<T>(this IIdentityServerBuilder builder)
        where T : class, IAuthorizeInteractionResponseGenerator
    {
        builder.Services.AddTransient<IAuthorizeInteractionResponseGenerator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the custom authorize request validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddCustomAuthorizeRequestValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ICustomAuthorizeRequestValidator
    {
        builder.Services.AddTransient<ICustomAuthorizeRequestValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the custom authorize request validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddCustomTokenRequestValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ICustomTokenRequestValidator
    {
        builder.Services.AddTransient<ICustomTokenRequestValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the custom backchannel authentication request validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddCustomBackchannelAuthenticationRequestValidator<T>(this IIdentityServerBuilder builder)
        where T : class, ICustomBackchannelAuthenticationValidator
    {
        builder.Services.AddTransient<ICustomBackchannelAuthenticationValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds support for client authentication using JWT bearer assertions.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddJwtBearerClientAuthentication(this IIdentityServerBuilder builder)
    {
        builder.AddSecretParser<JwtBearerClientAssertionSecretParser>();
        builder.AddSecretValidator<PrivateKeyJwtSecretValidator>();

        return builder;
    }

    /// <summary>
    /// Adds a client configuration validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddClientConfigurationValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IClientConfigurationValidator
    {
        builder.Services.AddTransient<IClientConfigurationValidator, T>();

        return builder;
    }


    /// <summary>
    /// Adds an IdentityProvider configuration validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityProviderConfigurationValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IIdentityProviderConfigurationValidator
    {
        builder.Services.AddTransient<IIdentityProviderConfigurationValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the X509 secret validators for mutual TLS.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddMutualTlsSecretValidators(this IIdentityServerBuilder builder)
    {
        builder.AddSecretParser<MutualTlsSecretParser>();
        builder.AddSecretValidator<X509ThumbprintSecretValidator>();
        builder.AddSecretValidator<X509NameSecretValidator>();

        return builder;
    }

    /// <summary>
    /// Adds a custom back-channel logout service.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddBackChannelLogoutService<T>(this IIdentityServerBuilder builder)
        where T : class, IBackChannelLogoutService
    {
        builder.Services.AddTransient<IBackChannelLogoutService, T>();

        return builder;
    }

    /// <summary>
    /// Adds configuration for the HttpClient used for back-channel logout notifications.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configureClient">The configuration callback.</param>
    /// <returns></returns>
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

            return new DefaultBackChannelLogoutHttpClient(httpClient, loggerFactory, new NoneCancellationTokenProvider());
        });

        return httpBuilder;
    }

    /// <summary>
    /// Adds configuration for the HttpClient used for JWT request_uri requests.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configureClient">The configuration callback.</param>
    /// <returns></returns>
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

            return new DefaultJwtRequestUriHttpClient(httpClient, options, loggerFactory, new NoneCancellationTokenProvider());
        });

        return httpBuilder;
    }

    /// <summary>
    /// Adds a custom authorization request parameter store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    [Obsolete("This feature is deprecated. Consider using Pushed Authorization Requests instead.")]
    public static IIdentityServerBuilder AddAuthorizationParametersMessageStore<T>(this IIdentityServerBuilder builder)
        where T : class, IAuthorizationParametersMessageStore
    {
        builder.Services.AddTransient<IAuthorizationParametersMessageStore, T>();

        return builder;
    }

    /// <summary>
    /// Adds a custom user session.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddUserSession<T>(this IIdentityServerBuilder builder)
        where T : class, IUserSession
    {
        // This is added as scoped due to the note regarding the AuthenticateAsync
        // method in the Duende.Services.DefaultUserSession implementation.
        builder.Services.AddScoped<IUserSession, T>();

        return builder;
    }


    /// <summary>
    /// Adds an identity provider store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddIdentityProviderStore<T>(this IIdentityServerBuilder builder)
        where T : class, IIdentityProviderStore
    {
        builder.Services.TryAddTransient<T>();
        builder.Services.TryAddTransient<ValidatingIdentityProviderStore<T>>();
        builder.Services.AddTransient<IIdentityProviderStore, NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<T>>>();

        return builder;
    }


    /// <summary>
    /// Adds the backchannel login user validator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddBackchannelAuthenticationUserValidator<T>(this IIdentityServerBuilder builder)
        where T : class, IBackchannelAuthenticationUserValidator
    {
        builder.Services.AddTransient<IBackchannelAuthenticationUserValidator, T>();

        return builder;
    }

    /// <summary>
    /// Adds the user notification service for backchannel login requests.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddBackchannelAuthenticationUserNotificationService<T>(this IIdentityServerBuilder builder)
        where T : class, IBackchannelAuthenticationUserNotificationService
    {
        builder.Services.AddTransient<IBackchannelAuthenticationUserNotificationService, T>();

        return builder;
    }

    /// <summary>
    /// Adds the legacy clock based on the pre-.NET8 ISystemClock.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddLegacyClock(this IIdentityServerBuilder builder)
    {
        builder.Services.AddTransient<IClock, LegacyClock>();

        return builder;
    }
}
