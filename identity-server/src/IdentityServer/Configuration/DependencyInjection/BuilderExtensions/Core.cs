// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Net;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Duende.IdentityServer.Endpoints;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Hosting.FederatedSignOut;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Licensing;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Logging;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Endpoints;
using Duende.IdentityServer.Saml.Infrastructure;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.Default;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Empty;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Validation;
using Duende.Private.Licencing.V2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder extension methods for registering core services
/// </summary>
public static class IdentityServerBuilderExtensionsCore
{
    /// <summary>
    /// Registers the fundamental ASP.NET Core platform services required by IdentityServer,
    /// including <see cref="IHttpContextAccessor"/>, the options infrastructure, and a named
    /// <see cref="HttpClient"/> factory. Also registers <see cref="IdentityServerOptions"/> and
    /// <c>PersistentGrantOptions</c> as resolvable singletons from the options system.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add platform services to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddRequiredPlatformServices(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        builder.Services.AddOptions();
        builder.Services.AddSingleton(
            resolver => resolver.GetRequiredService<IOptions<IdentityServerOptions>>().Value);
        builder.Services.AddTransient(
            resolver => resolver.GetRequiredService<IOptions<IdentityServerOptions>>().Value.PersistentGrants);
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IPostConfigureOptions<IdentityServerOptions>>(
            sp => new PostConfigureLicenseKey(sp.GetService<IConfiguration>() ?? new ConfigurationBuilder().Build()));

        return builder;
    }

    /// <summary>
    /// Adds the default infrastructure for cookie authentication in IdentityServer.
    /// Registers the default and external cookie authentication schemes and the necessary
    /// decorators and post-configuration hooks required for IdentityServer's session management.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add cookie authentication to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddCookieAuthentication(this IIdentityServerBuilder builder) => builder
            .AddDefaultCookieHandlers()
            .AddCookieAuthenticationExtensions();

    /// <summary>
    /// Adds the default cookie handlers and corresponding configuration.
    /// Registers the IdentityServer default cookie scheme and the external cookie scheme used
    /// for temporarily holding external identity provider claims during sign-in.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add cookie handlers to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddDefaultCookieHandlers(this IIdentityServerBuilder builder)
    {
        builder.Services.AddAuthentication(IdentityServerConstants.DefaultCookieAuthenticationScheme)
            .AddCookie(IdentityServerConstants.DefaultCookieAuthenticationScheme)
            .AddCookie(IdentityServerConstants.ExternalCookieAuthenticationScheme);
        builder.Services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, ConfigureInternalCookieOptions>();

        return builder;
    }

    /// <summary>
    /// Adds the necessary decorators for cookie authentication required by IdentityServer.
    /// Registers post-configuration for cookie options, and decorates <see cref="IAuthenticationService"/>
    /// and <see cref="IAuthenticationHandlerProvider"/> to support federated sign-out.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add cookie authentication extensions to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddCookieAuthenticationExtensions(this IIdentityServerBuilder builder)
    {
        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, PostConfigureInternalCookieOptions>();
        builder.Services.AddTransientDecorator<IAuthenticationService, IdentityServerAuthenticationService>();
        builder.Services.AddTransientDecorator<IAuthenticationHandlerProvider, FederatedSignoutAuthenticationHandlerProvider>();

        return builder;
    }

    /// <summary>
    /// Registers all default IdentityServer protocol endpoints and their corresponding HTTP response writers.
    /// Endpoints include: authorize, token, discovery, userinfo, end-session, introspection, revocation,
    /// device authorization, backchannel authentication, pushed authorization, check-session, and JWKS.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add endpoints to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddDefaultEndpoints(this IIdentityServerBuilder builder)
    {
        builder.Services.AddTransient<IEndpointRouter, EndpointRouter>();

        builder.AddEndpoint<AuthorizeCallbackEndpoint>(EndpointNames.Authorize, ProtocolRoutePaths.AuthorizeCallback.EnsureLeadingSlash());
        builder.AddEndpoint<AuthorizeEndpoint>(EndpointNames.Authorize, ProtocolRoutePaths.Authorize.EnsureLeadingSlash());
        builder.AddEndpoint<BackchannelAuthenticationEndpoint>(EndpointNames.BackchannelAuthentication, ProtocolRoutePaths.BackchannelAuthentication.EnsureLeadingSlash());
        builder.AddEndpoint<CheckSessionEndpoint>(EndpointNames.CheckSession, ProtocolRoutePaths.CheckSession.EnsureLeadingSlash());
        builder.AddEndpoint<DeviceAuthorizationEndpoint>(EndpointNames.DeviceAuthorization, ProtocolRoutePaths.DeviceAuthorization.EnsureLeadingSlash());
        builder.AddEndpoint<DiscoveryKeyEndpoint>(EndpointNames.Jwks, ProtocolRoutePaths.DiscoveryWebKeys.EnsureLeadingSlash());
        builder.AddEndpoint<DiscoveryEndpoint>(EndpointNames.Discovery, ProtocolRoutePaths.DiscoveryConfiguration.EnsureLeadingSlash());
        builder.AddEndpoint<OAuthMetadataEndpoint>(EndpointNames.OAuthMetadata, ProtocolRoutePaths.OAuthMetadata.EnsureLeadingSlash(), EndpointHelpers.OAuthMetadataHelpers.IsMatch);
        builder.AddEndpoint<EndSessionCallbackEndpoint>(EndpointNames.EndSession, ProtocolRoutePaths.EndSessionCallback.EnsureLeadingSlash());
        builder.AddEndpoint<EndSessionEndpoint>(EndpointNames.EndSession, ProtocolRoutePaths.EndSession.EnsureLeadingSlash());
        builder.AddEndpoint<IntrospectionEndpoint>(EndpointNames.Introspection, ProtocolRoutePaths.Introspection.EnsureLeadingSlash());
        builder.AddEndpoint<PushedAuthorizationEndpoint>(EndpointNames.PushedAuthorization, ProtocolRoutePaths.PushedAuthorization.EnsureLeadingSlash());
        builder.AddEndpoint<TokenRevocationEndpoint>(EndpointNames.Revocation, ProtocolRoutePaths.Revocation.EnsureLeadingSlash());
        builder.AddEndpoint<TokenEndpoint>(EndpointNames.Token, ProtocolRoutePaths.Token.EnsureLeadingSlash());
        builder.AddEndpoint<UserInfoEndpoint>(EndpointNames.UserInfo, ProtocolRoutePaths.UserInfo.EnsureLeadingSlash());
        builder.AddEndpoint<SpLogoutCompletionEndpoint>(EndpointNames.SamlSpLogoutCompletion, SamlConstants.Defaults.SpLogoutCompletionPath.EnsureLeadingSlash());

        builder.AddHttpWriter<AuthorizeInteractionPageResult, AuthorizeInteractionPageHttpWriter>();
        builder.AddHttpWriter<AuthorizeResult, AuthorizeHttpWriter>();
        builder.AddHttpWriter<BackchannelAuthenticationResult, BackchannelAuthenticationHttpWriter>();
        builder.AddHttpWriter<BadRequestResult, BadRequestHttpWriter>();
        builder.AddHttpWriter<CheckSessionResult, CheckSessionHttpWriter>();
        builder.AddHttpWriter<DeviceAuthorizationResult, DeviceAuthorizationHttpWriter>();
        builder.AddHttpWriter<DiscoveryDocumentResult, DiscoveryDocumentHttpWriter>();
        builder.AddHttpWriter<EndSessionCallbackResult, EndSessionCallbackHttpWriter>();
        builder.AddHttpWriter<EndSessionResult, EndSessionHttpWriter>();
        builder.AddHttpWriter<IntrospectionResult, IntrospectionHttpWriter>();
        builder.AddHttpWriter<JsonWebKeysResult, JsonWebKeysHttpWriter>();
        builder.AddHttpWriter<ProtectedResourceErrorResult, ProtectedResourceErrorHttpWriter>();
        builder.AddHttpWriter<PushedAuthorizationResult, PushedAuthorizationHttpWriter>();
        builder.AddHttpWriter<PushedAuthorizationErrorResult, PushedAuthorizationErrorHttpWriter>();
        builder.AddHttpWriter<StatusCodeResult, StatusCodeHttpWriter>();
        builder.AddHttpWriter<TokenErrorResult, TokenErrorHttpWriter>();
        builder.AddHttpWriter<TokenResult, TokenHttpWriter>();
        builder.AddHttpWriter<TokenRevocationErrorResult, TokenRevocationErrorHttpWriter>();
        builder.AddHttpWriter<UserInfoResult, UserInfoHttpWriter>();

        return builder;
    }

    /// <summary>
    /// Registers a custom protocol endpoint handler and maps it to the specified path.
    /// The endpoint is registered as a transient service and added to the endpoint routing table.
    /// </summary>
    /// <typeparam name="TEndpoint">The <see cref="IEndpointHandler"/> implementation to register.</typeparam>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the endpoint to.</param>
    /// <param name="name">The logical name of the endpoint (e.g. <c>EndpointNames.Authorize</c>).</param>
    /// <param name="path">The URL path at which the endpoint is served.</param>
    /// <param name="isMatch">An optional custom matching function for the endpoint. Defaults to <see langword="null"/>,
    /// which uses the default path-based matching algorithm.</param>
    public static IIdentityServerBuilder AddEndpoint<TEndpoint>(this IIdentityServerBuilder builder, string name, PathString path, Func<HttpContext, bool>? isMatch = null)
        where TEndpoint : class, IEndpointHandler
    {
        builder.Services.AddTransient<TEndpoint>();
        var endpoint = new Duende.IdentityServer.Hosting.Endpoint(name, path, typeof(TEndpoint));
        if (isMatch is not null)
        {
            endpoint.IsMatch = isMatch;
        }

        builder.Services.AddSingleton(endpoint);

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IHttpResponseWriter{T}"/> for a specific <see cref="IEndpointResult"/> type,
    /// allowing customization of how a particular endpoint result is serialized to the HTTP response.
    /// </summary>
    public static IIdentityServerBuilder AddHttpWriter<TResult, TWriter>(this IIdentityServerBuilder builder)
        where TResult : class, IEndpointResult
        where TWriter : class, IHttpResponseWriter<TResult>
    {
        builder.Services.AddTransient<IHttpResponseWriter<TResult>, TWriter>();
        return builder;
    }

    /// <summary>
    /// Registers the core IdentityServer services that are not protocol-endpoint-specific.
    /// This includes server URL helpers, issuer name resolution, secret parsing and validation pipelines,
    /// extension grant validation, JWT request validation, user session management, CORS infrastructure,
    /// SAML no-op stubs, concurrency locks, empty default stores, licensing, and diagnostic services.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add core services to.</param>
    public static IIdentityServerBuilder AddCoreServices(this IIdentityServerBuilder builder)
    {
        builder.Services.AddTransient<IServerUrls, DefaultServerUrls>();
        builder.Services.AddTransient<IMtlsEndpointGenerator, DefaultMtlsEndpointGenerator>();
        builder.Services.AddTransient<IIssuerNameService, DefaultIssuerNameService>();
        builder.Services.AddTransient<ISecretsListParser, SecretParser>();
        builder.Services.AddTransient<ISecretsListValidator, SecretValidator>();
        builder.Services.AddTransient<ExtensionGrantValidator>();
        builder.Services.AddTransient<BearerTokenUsageValidator>();
        builder.Services.AddTransient<IJwtRequestValidator, JwtRequestValidator>();
        builder.Services.AddTransient<ReturnUrlParser>();
        builder.Services.AddTransient<IIdentityServerTools, IdentityServerTools>();
        builder.Services.AddTransient<IReturnUrlParser, OidcReturnUrlParser>();
        builder.Services.AddScoped<IUserSession, DefaultUserSession>();
        builder.Services.AddTransient(typeof(MessageCookie<>));
        builder.Services.AddTransient(typeof(SanitizedLogger<>));

        builder.Services.AddCors();
        builder.Services.AddTransientDecorator<ICorsPolicyProvider, CorsPolicyProvider>();

        builder.Services.TryAddTransient<IBackchannelAuthenticationUserValidator, NopBackchannelAuthenticationUserValidator>();

        // Register no-op SAML services for services used in logout paths
        // These are replaced by actual implementations in AddSaml and ISamlServiceProviderStore
        // can be replaced with a call to AddSamlServiceProviderStore
        builder.Services.TryAddTransient<ISamlServiceProviderStore, EmptySamlServiceProviderStore>();
        builder.Services.TryAddScoped<ISamlLogoutNotificationService, NopSamlLogoutNotificationService>();

        // SP logout completion endpoint dependencies — registered here so the endpoint
        // is available for both the static AddSamlServiceProvider() path and the
        // AddSaml()/AddSamlDynamicProvider() paths.
        builder.Services.TryAddSingleton<RsaCertificateFactory>();
        builder.Services.TryAddSingleton<ISamlLogoutSessionStore, InMemorySamlLogoutSessionStore>();
        builder.Services.TryAddScoped<ISamlSigningService, SamlSigningService>();
        builder.Services.TryAddTransient<ISamlXmlWriter, SamlXmlWriter>();
        builder.Services.TryAddTransient<ISaml2IssuerNameService, Saml2IssuerNameService>();
        builder.Services.TryAddTransient<ISaml2SloResponseGenerator, Saml2SloResponseGenerator>();

        builder.Services.TryAddTransient<IConnectedApplicationStore, ConnectedApplicationStore>();

        builder.Services.TryAddSingleton(typeof(IConcurrencyLock<>), typeof(DefaultConcurrencyLock<>));

        builder.Services.TryAddTransient<IClientStore, EmptyClientStore>();
        builder.Services.TryAddTransient<IResourceStore, EmptyResourceStore>();

        builder.Services.AddSingleton(resolver =>
        {
            var jsonResolver = new PolymorphicJsonTypeResolver();
            var identityServerOptions = resolver.GetRequiredService<IOptions<IdentityServerOptions>>().Value;
            var registration = jsonResolver.AddPolymorphicType<IdentityProvider>("$type");
            foreach (var (type, providerType) in identityServerOptions.DynamicProviders.ProviderTypes)
            {
                registration.AddDerivedType(providerType.IdentityProviderType, type);
            }
            return jsonResolver;
        });

        builder.Services.TryAddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<V2LicenseAccessor>>();
            var options = sp.GetRequiredService<IOptions<IdentityServerOptions>>().Value;
            var accessor = new V2LicenseAccessor(() => options.LicenseKey, logger);
            return accessor.Current;
        });

        builder.Services.TryAddSingleton(sp =>
        {
            var license = sp.GetRequiredService<V2License>();
            return new LicenseInformation
            {
                CompanyName = license.IsConfigured ? license.CompanyName : null,
                ContactInfo = license.IsConfigured ? license.ContactInfo : null,
                SerialNumber = license.IsConfigured ? license.SerialNumber : null,
                IssuedAt = license.IsConfigured ? license.IssuedAt : null,
                Expiration = license.IsConfigured ? license.Expiration : null,
                IsConfigured = license.IsConfigured,
                EntitledSkus = license.IsConfigured
                    ? license.Entitlements.Select(e => Skus.Get(e.SkuId)?.Name).OfType<string>().ToList().AsReadOnly()
                    : (IReadOnlyCollection<string>)[]
            };
        });

        builder.Services.TryAddSingleton<LicenseValidator>();
        builder.Services.TryAddSingleton<IdentityServerLicenseValidator>();
        builder.Services.AddSingleton<LicenseUsageTracker>();

        builder.Services.AddSingleton<IDiagnosticEntry, AssemblyInfoDiagnosticEntry>();
        builder.Services.AddSingleton<IDiagnosticEntry, AuthSchemeInfoDiagnosticEntry>();
        builder.Services.AddSingleton(new ServiceCollectionAccessor(builder.Services));
        builder.Services.AddSingleton<IDiagnosticEntry, RegisteredImplementationsDiagnosticEntry>();
        builder.Services.AddSingleton<IDiagnosticEntry, IdentityServerOptionsDiagnosticEntry>();
        builder.Services.AddSingleton<IDiagnosticEntry, DataProtectionDiagnosticEntry>();
        builder.Services.AddSingleton<IDiagnosticEntry, TokenIssueCountDiagnosticEntry>();
        builder.Services.AddSingleton<IDiagnosticEntry, LicenseUsageDiagnosticEntry>();
        builder.Services.AddSingleton<IDiagnosticEntry>(new BasicServerInfoDiagnosticEntry(Dns.GetHostName));
        builder.Services.AddSingleton<IDiagnosticEntry, EndpointUsageDiagnosticEntry>();
        builder.Services.AddSingleton<ClientLoadedTracker>();
        builder.Services.AddSingleton<IDiagnosticEntry, ClientInfoDiagnosticEntry>();
        builder.Services.AddSingleton<ResourceLoadedTracker>();
        builder.Services.AddSingleton<IDiagnosticEntry, ResourceInfoDiagnosticEntry>();
        builder.Services.AddSingleton<DiagnosticSummary>();
        builder.Services.AddSingleton(serviceProvider => new DiagnosticDataService(
            serviceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
            serviceProvider.GetServices<IDiagnosticEntry>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        builder.Services.AddHostedService<DiagnosticHostedService>();

        return builder;
    }

    /// <summary>
    /// Registers the default implementations of all pluggable IdentityServer services.
    /// This includes token creation and validation, claims generation, refresh token handling,
    /// consent management, CORS policy, profile service, event sink, device flow, backchannel
    /// authentication throttling, pushed authorization, session coordination, and HTTP clients
    /// for back-channel logout and JWT request URI fetching.
    /// All registrations use <c>TryAdd</c> so they can be replaced by custom implementations.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add pluggable services to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddPluggableServices(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddTransient<IPersistedGrantService, DefaultPersistedGrantService>();
        builder.Services.TryAddTransient<IKeyMaterialService, DefaultKeyMaterialService>();
        builder.Services.TryAddTransient<ITokenService, DefaultTokenService>();
        builder.Services.TryAddTransient<ITokenCreationService, DefaultTokenCreationService>();
        builder.Services.TryAddTransient<IClaimsService, DefaultClaimsService>();
        builder.Services.TryAddTransient<IRefreshTokenService, DefaultRefreshTokenService>();
        builder.Services.TryAddTransient<IDeviceFlowCodeService, DefaultDeviceFlowCodeService>();
        builder.Services.TryAddTransient<IConsentService, DefaultConsentService>();
        builder.Services.TryAddTransient<ICorsPolicyService, DefaultCorsPolicyService>();
        builder.Services.TryAddTransient<IProfileService, DefaultProfileService>();
        builder.Services.TryAddTransient<IConsentMessageStore, ConsentMessageStore>();
        builder.Services.TryAddTransient<IMessageStore<LogoutMessage>, ProtectedDataMessageStore<LogoutMessage>>();
        builder.Services.TryAddTransient<IMessageStore<LogoutNotificationContext>, ProtectedDataMessageStore<LogoutNotificationContext>>();
        builder.Services.TryAddTransient<IMessageStore<ErrorMessage>, ProtectedDataMessageStore<ErrorMessage>>();
        builder.Services.TryAddTransient<IMessageStore<SamlSpLogoutMessage>, ProtectedDataMessageStore<Duende.IdentityServer.Hosting.FederatedSignOut.SamlSpLogoutMessage>>();
        builder.Services.TryAddTransient<IIdentityServerInteractionService, DefaultIdentityServerInteractionService>();
        builder.Services.TryAddTransient<IDeviceFlowInteractionService, DefaultDeviceFlowInteractionService>();
        builder.Services.TryAddTransient<IBackchannelAuthenticationInteractionService, DefaultBackchannelAuthenticationInteractionService>();
        builder.Services.TryAddTransient<IAuthorizationCodeStore, DefaultAuthorizationCodeStore>();
        builder.Services.TryAddTransient<IRefreshTokenStore, DefaultRefreshTokenStore>();
        builder.Services.TryAddTransient<IReferenceTokenStore, DefaultReferenceTokenStore>();
        builder.Services.TryAddTransient<IUserConsentStore, DefaultUserConsentStore>();
        builder.Services.TryAddTransient<IBackChannelAuthenticationRequestStore, DefaultBackChannelAuthenticationRequestStore>();
        builder.Services.TryAddTransient<IHandleGenerationService, DefaultHandleGenerationService>();
        builder.Services.TryAddTransient<IPersistentGrantSerializer, PersistentGrantSerializer>();
        builder.Services.TryAddTransient<IPushedAuthorizationSerializer, PushedAuthorizationSerializer>();
        builder.Services.TryAddTransient<IPushedAuthorizationService, PushedAuthorizationService>();
        builder.Services.TryAddTransient<IEventService, DefaultEventService>();
        builder.Services.TryAddTransient<IEventSink, DefaultEventSink>();
        builder.Services.TryAddTransient<IUserCodeService, DefaultUserCodeService>();
        builder.Services.TryAddTransient<IUserCodeGenerator, NumericUserCodeGenerator>();
        builder.Services.TryAddTransient<ILogoutNotificationService, LogoutNotificationService>();
        builder.Services.TryAddTransient<IBackChannelLogoutService, DefaultBackChannelLogoutService>();
        builder.Services.TryAddTransient<IScopeParser, DefaultScopeParser>();
        builder.Services.TryAddTransient<ISessionCoordinationService, DefaultSessionCoordinationService>();
        builder.Services.TryAddTransient<IReplayCache, DefaultReplayCache>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddTransient<IUiLocalesService, DefaultUiLocalesService>();

        builder.Services.TryAddTransient<IBackchannelAuthenticationThrottlingService, DistributedBackchannelAuthenticationThrottlingService>();
        builder.Services.TryAddTransient<IBackchannelAuthenticationUserNotificationService, NopBackchannelAuthenticationUserNotificationService>();

        builder.AddJwtRequestUriHttpClient();
        builder.AddBackChannelLogoutHttpClient();

        builder.Services.AddTransient<IClientSecretValidator, ClientSecretValidator>();
        builder.Services.AddTransient<IApiSecretValidator, ApiSecretValidator>();

        builder.Services.TryAddTransient<IDeviceFlowThrottlingService, DistributedDeviceFlowThrottlingService>();
        builder.Services.AddDistributedMemoryCache();

        return builder;
    }

    /// <summary>
    /// Registers the automatic key management services used by IdentityServer to create, rotate,
    /// and retire signing keys without manual intervention. Includes the key manager, key store
    /// (defaulting to the file system), key protector (using ASP.NET Core Data Protection),
    /// and an in-memory key store cache.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add key management services to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddKeyManagement(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddTransient<IAutomaticKeyManagerKeyStore, AutomaticKeyManagerKeyStore>();
        builder.Services.TryAddTransient<IKeyManager, KeyManager>();
        builder.Services.TryAddTransient<ISigningKeyProtector, DataProtectionKeyProtector>();
        builder.Services.TryAddSingleton<ISigningKeyStoreCache, InMemoryKeyStoreCache>();
        builder.Services.TryAddTransient(provider => provider.GetRequiredService<IdentityServerOptions>().KeyManagement);

        builder.Services.TryAddSingleton<ISigningKeyStore>(x =>
        {
            var opts = x.GetRequiredService<IdentityServerOptions>();
            return new FileSystemKeyStore(opts.KeyManagement.KeyPath, x.GetRequiredService<ILogger<FileSystemKeyStore>>());
        });

        return builder;
    }

    /// <summary>
    /// Registers the core infrastructure for the dynamic external identity providers feature.
    /// This includes a dynamic <see cref="IAuthenticationSchemeProvider"/> decorator that loads
    /// provider schemes on demand, a no-op <see cref="IIdentityProviderStore"/> default, and
    /// per-request and singleton caches for dynamically loaded authentication schemes.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add dynamic provider core services to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddDynamicProvidersCore(this IIdentityServerBuilder builder)
    {
        builder.Services.AddTransient(svcs => svcs.GetRequiredService<IdentityServerOptions>().DynamicProviders);
        builder.Services.AddTransientDecorator<IAuthenticationSchemeProvider, DynamicAuthenticationSchemeProvider>();
        builder.Services.TryAddSingleton<IIdentityProviderStore, NopIdentityProviderStore>();
        builder.Services.TryAddSingleton<IdentityProviderOptionsMonitorCache>();
        builder.Services.TryAddSingleton<IHybridCacheSerializer<IdentityProvider>, PolymorphicHybridCacheSerializer<IdentityProvider>>();
        builder.Services.TryAddSingleton<IIdentityProviderFactory, DynamicIdentityProviderFactory>();
        // the per-request cache is to ensure that a scheme loaded from the cache is still available later in the
        // request and made available anywhere else during this request (in case the static cache times out across
        // 2 calls within the same request)
        builder.Services.AddScoped<DynamicAuthenticationSchemeCache>();

        return builder;
    }

    /// <summary>
    /// Registers the default implementations of all request validators used by IdentityServer's protocol endpoints.
    /// Includes validators for authorization, token, end-session, introspection, revocation, device authorization,
    /// backchannel authentication, pushed authorization, resource owner password, redirect URIs, DPoP proofs,
    /// and client/identity provider configuration. All registrations use <c>TryAdd</c> so they can be replaced.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add validators to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddValidators(this IIdentityServerBuilder builder)
    {
        // core
        builder.Services.TryAddTransient<IEndSessionRequestValidator, EndSessionRequestValidator>();
        builder.Services.TryAddTransient<ITokenRevocationRequestValidator, TokenRevocationRequestValidator>();
        builder.Services.TryAddTransient<IAuthorizeRequestValidator, AuthorizeRequestValidator>();
        builder.Services.TryAddTransient<IRequestObjectValidator, RequestObjectValidator>();
        builder.Services.TryAddTransient<ITokenRequestValidator, TokenRequestValidator>();
        builder.Services.TryAddTransient<IRedirectUriValidator, StrictRedirectUriValidator>();
        builder.Services.TryAddTransient<ITokenValidator, TokenValidator>();
        builder.Services.TryAddTransient<IIntrospectionRequestValidator, IntrospectionRequestValidator>();
        builder.Services.TryAddTransient<IResourceOwnerPasswordValidator, NotSupportedResourceOwnerPasswordValidator>();
        builder.Services.TryAddTransient<ICustomTokenRequestValidator, DefaultCustomTokenRequestValidator>();
        builder.Services.TryAddTransient<IUserInfoRequestValidator, UserInfoRequestValidator>();
        builder.Services.TryAddTransient<IClientConfigurationValidator, DefaultClientConfigurationValidator>();
        builder.Services.TryAddTransient<IIdentityProviderConfigurationValidator, DefaultIdentityProviderConfigurationValidator>();
        builder.Services.TryAddTransient<IDeviceAuthorizationRequestValidator, DeviceAuthorizationRequestValidator>();
        builder.Services.TryAddTransient<IDeviceCodeValidator, DeviceCodeValidator>();
        builder.Services.TryAddTransient<IBackchannelAuthenticationRequestIdValidator, BackchannelAuthenticationRequestIdValidator>();
        builder.Services.TryAddTransient<IResourceValidator, DefaultResourceValidator>();
        builder.Services.TryAddTransient<IDPoPProofValidator, DefaultDPoPProofValidator>();
        builder.Services.TryAddTransient<IBackchannelAuthenticationRequestValidator, BackchannelAuthenticationRequestValidator>();
        builder.Services.TryAddTransient<IPushedAuthorizationRequestValidator, PushedAuthorizationRequestValidator>();
        builder.Services.TryAddTransient<IIssuerPathValidator, DefaultIssuerPathValidator>();

        // optional
        builder.Services.TryAddTransient<ICustomTokenValidator, DefaultCustomTokenValidator>();
        builder.Services.TryAddTransient<ICustomAuthorizeRequestValidator, DefaultCustomAuthorizeRequestValidator>();
        builder.Services.TryAddTransient<ICustomBackchannelAuthenticationValidator, DefaultCustomBackchannelAuthenticationValidator>();

        return builder;
    }

    /// <summary>
    /// Registers the default response generators for all IdentityServer protocol endpoints.
    /// Includes generators for token, userinfo, introspection, authorize, discovery, revocation,
    /// device authorization, backchannel authentication, and pushed authorization responses.
    /// All registrations use <c>TryAdd</c> so they can be replaced by custom implementations.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add response generators to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddResponseGenerators(this IIdentityServerBuilder builder)
    {
        builder.Services.TryAddTransient<ITokenResponseGenerator, TokenResponseGenerator>();
        builder.Services.TryAddTransient<IUserInfoResponseGenerator, UserInfoResponseGenerator>();
        builder.Services.TryAddTransient<IIntrospectionResponseGenerator, IntrospectionResponseGenerator>();
        builder.Services.TryAddTransient<IAuthorizeInteractionResponseGenerator, AuthorizeInteractionResponseGenerator>();
        builder.Services.TryAddTransient<IAuthorizeResponseGenerator, AuthorizeResponseGenerator>();
        builder.Services.TryAddTransient<IDiscoveryResponseGenerator, DiscoveryResponseGenerator>();
        builder.Services.TryAddTransient<ITokenRevocationResponseGenerator, TokenRevocationResponseGenerator>();
        builder.Services.TryAddTransient<IDeviceAuthorizationResponseGenerator, DeviceAuthorizationResponseGenerator>();
        builder.Services.TryAddTransient<IBackchannelAuthenticationResponseGenerator, BackchannelAuthenticationResponseGenerator>();
        builder.Services.TryAddTransient<IPushedAuthorizationResponseGenerator, PushedAuthorizationResponseGenerator>();

        return builder;
    }

    /// <summary>
    /// Registers the default secret parsers for extracting client credentials from incoming requests.
    /// Adds <c>BasicAuthenticationSecretParser</c> (HTTP Basic authentication header) and
    /// <c>PostBodySecretParser</c> (form-encoded request body) as the default parsers.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add secret parsers to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddDefaultSecretParsers(this IIdentityServerBuilder builder)
    {
        builder.Services.AddTransient<ISecretParser, BasicAuthenticationSecretParser>();
        builder.Services.AddTransient<ISecretParser, PostBodySecretParser>();

        return builder;
    }

    /// <summary>
    /// Registers the default secret validator for verifying client credentials.
    /// Adds <c>HashedSharedSecretValidator</c>, which validates shared secrets stored as SHA-256 or SHA-512 hashes.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add secret validators to.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddDefaultSecretValidators(this IIdentityServerBuilder builder)
    {
        builder.Services.AddTransient<ISecretValidator, HashedSharedSecretValidator>();

        return builder;
    }

    /// <summary>
    /// Adds the license summary, which provides information about the current license usage.
    /// </summary>
    public static IIdentityServerBuilder AddLicenseSummary(this IIdentityServerBuilder builder)
    {
        builder.Services.AddTransient<LicenseUsageSummary>(services => services.GetRequiredService<LicenseUsageTracker>().GetSummary());
        return builder;
    }

    internal static void AddTransientDecorator<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddDecorator<TService>();
        services.AddTransient<TService, TImplementation>();
    }

    internal static void AddDecorator<TService>(this IServiceCollection services)
    {
        var registration = services.LastOrDefault(x => x.ServiceType == typeof(TService));
        if (registration == null)
        {
            throw new InvalidOperationException("Service type: " + typeof(TService).Name + " not registered.");
        }
        if (services.Any(x => x.ServiceType == typeof(Decorator<TService>)))
        {
            throw new InvalidOperationException("Decorator already registered for type: " + typeof(TService).Name + ".");
        }

        services.Remove(registration);

        if (registration.ImplementationInstance != null)
        {
            var type = registration.ImplementationInstance.GetType();
            var innerType = typeof(Decorator<,>).MakeGenericType(typeof(TService), type);
            services.Add(new ServiceDescriptor(typeof(Decorator<TService>), innerType, ServiceLifetime.Transient));
            services.Add(new ServiceDescriptor(type, registration.ImplementationInstance));
        }
        else if (registration.ImplementationFactory != null)
        {
            services.Add(new ServiceDescriptor(typeof(Decorator<TService>), provider =>
            {
                return new DisposableDecorator<TService>((TService)registration.ImplementationFactory(provider));
            }, registration.Lifetime));
        }
        else if (registration.ImplementationType != null)
        {
            var type = registration.ImplementationType;
            var innerType = typeof(Decorator<,>).MakeGenericType(typeof(TService), registration.ImplementationType);
            services.Add(new ServiceDescriptor(typeof(Decorator<TService>), innerType, ServiceLifetime.Transient));
            services.Add(new ServiceDescriptor(type, type, registration.Lifetime));
        }
        else
        {
            throw new InvalidOperationException("Invalid registration in DI for: " + typeof(TService).Name);
        }
    }
}
