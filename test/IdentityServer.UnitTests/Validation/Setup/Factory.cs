// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.Default;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Validation;
using UnitTests.Common;
using Microsoft.Extensions.Logging;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer;
using Duende.IdentityServer.Licensing.V2;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Validation.Setup;

internal static class Factory
{
    public static IClientStore CreateClientStore()
    {
        return new InMemoryClientStore(TestClients.Get());
    }

    public static TokenRequestValidator CreateTokenRequestValidator(
        IdentityServerOptions options = null,
        IIssuerNameService issuerNameService = null,
        IServerUrls serverUrls = null,
        IResourceStore resourceStore = null,
        IAuthorizationCodeStore authorizationCodeStore = null,
        IRefreshTokenStore refreshTokenStore = null,
        IResourceOwnerPasswordValidator resourceOwnerValidator = null,
        IProfileService profile = null,
        IDeviceCodeValidator deviceCodeValidator = null,
        IBackchannelAuthenticationRequestIdValidator backchannelAuthenticationRequestIdValidator = null,
        IEnumerable<IExtensionGrantValidator> extensionGrantValidators = null,
        ICustomTokenRequestValidator customRequestValidator = null,
        IRefreshTokenService refreshTokenService = null,
        IResourceValidator resourceValidator = null)
    {
        if (options == null)
        {
            options = TestIdentityServerOptions.Create();
        }

        if (issuerNameService == null)
        {
            issuerNameService = new TestIssuerNameService(options.IssuerUri);
        }

        if (serverUrls == null)
        {
            serverUrls = new MockServerUrls()
            {
                Origin = options.IssuerUri ?? "https://identityserver",
            };
        }

        if (resourceStore == null)
        {
            resourceStore = new InMemoryResourcesStore(TestScopes.GetIdentity(), TestScopes.GetApis(), TestScopes.GetScopes());
        }

        if (resourceOwnerValidator == null)
        {
            resourceOwnerValidator = new TestResourceOwnerPasswordValidator();
        }

        if (profile == null)
        {
            profile = new TestProfileService();
        }

        if (deviceCodeValidator == null)
        {
            deviceCodeValidator = new TestDeviceCodeValidator();
        }

        if (backchannelAuthenticationRequestIdValidator == null)
        {
            backchannelAuthenticationRequestIdValidator = new TestBackchannelAuthenticationRequestIdValidator();
        }

        if (customRequestValidator == null)
        {
            customRequestValidator = new DefaultCustomTokenRequestValidator();
        }

        ExtensionGrantValidator aggregateExtensionGrantValidator;
        if (extensionGrantValidators == null)
        {
            aggregateExtensionGrantValidator = new ExtensionGrantValidator(new[] { new TestGrantValidator() }, TestLogger.Create<ExtensionGrantValidator>());
        }
        else
        {
            aggregateExtensionGrantValidator = new ExtensionGrantValidator(extensionGrantValidators, TestLogger.Create<ExtensionGrantValidator>());
        }

        if (authorizationCodeStore == null)
        {
            authorizationCodeStore = CreateAuthorizationCodeStore();
        }

        if (refreshTokenStore == null)
        {
            refreshTokenStore = CreateRefreshTokenStore();
        }

        if (resourceValidator == null)
        {
            resourceValidator = CreateResourceValidator(resourceStore);
        }

        if (refreshTokenService == null)
        {
            refreshTokenService = CreateRefreshTokenService(
                refreshTokenStore,
                profile);
        }

        return new TokenRequestValidator(
            options,
            issuerNameService,
            serverUrls,
            authorizationCodeStore,
            resourceOwnerValidator,
            profile,
            deviceCodeValidator,
            backchannelAuthenticationRequestIdValidator,
            aggregateExtensionGrantValidator,
            customRequestValidator,
            resourceValidator,
            resourceStore,
            refreshTokenService,
            new DefaultDPoPProofValidator(options, new MockReplayCache(), new StubClock(), new StubDataProtectionProvider(), new LoggerFactory().CreateLogger<DefaultDPoPProofValidator>()),
            new TestEventService(),
            new StubClock(),
            new LicenseUsageTracker(new LicenseAccessor(new IdentityServerOptions(), NullLogger<LicenseAccessor>.Instance)),
            TestLogger.Create<TokenRequestValidator>());
    }

    public static IRefreshTokenService CreateRefreshTokenService(IRefreshTokenStore store = null, IProfileService profile = null)
    {
        return CreateRefreshTokenService(store ?? CreateRefreshTokenStore(), 
            profile ?? new TestProfileService(), 
            new PersistentGrantOptions());
    }

    private static IRefreshTokenService CreateRefreshTokenService(
        IRefreshTokenStore store, 
        IProfileService profile,
        PersistentGrantOptions options)
    {
        var service = new DefaultRefreshTokenService(
            store,
            profile,
            new StubClock(),
            options,
            TestLogger.Create<DefaultRefreshTokenService>());

        return service;
    }

    internal static IResourceValidator CreateResourceValidator(IResourceStore store = null)
    {
        store = store ?? new InMemoryResourcesStore(TestScopes.GetIdentity(), TestScopes.GetApis(), TestScopes.GetScopes());
        return new DefaultResourceValidator(store, new DefaultScopeParser(TestLogger.Create<DefaultScopeParser>()), TestLogger.Create<DefaultResourceValidator>());
    }

    internal static ITokenCreationService CreateDefaultTokenCreator(IdentityServerOptions options = null)
    {
        return new DefaultTokenCreationService(
            new StubClock(),
            new DefaultKeyMaterialService(
                new IValidationKeysStore[] { },
                new ISigningCredentialStore[] { new InMemorySigningCredentialsStore(TestCert.LoadSigningCredentials()) },
                new NopAutomaticKeyManagerKeyStore()
            ),
            options ?? TestIdentityServerOptions.Create(),
            TestLogger.Create<DefaultTokenCreationService>());
    }

    public static DeviceAuthorizationRequestValidator CreateDeviceAuthorizationRequestValidator(
        IdentityServerOptions options = null,
        IResourceStore resourceStore = null,
        IResourceValidator resourceValidator = null)
    {
        if (options == null)
        {
            options = TestIdentityServerOptions.Create();
        }
            
        if (resourceStore == null)
        {
            resourceStore = new InMemoryResourcesStore(TestScopes.GetIdentity(), TestScopes.GetApis(), TestScopes.GetScopes());
        }

        if (resourceValidator == null)
        {
            resourceValidator = CreateResourceValidator(resourceStore);
        }


        return new DeviceAuthorizationRequestValidator(
            options,
            resourceValidator,
            TestLogger.Create<DeviceAuthorizationRequestValidator>());
    }

    public static AuthorizeRequestValidator CreateAuthorizeRequestValidator(
        IdentityServerOptions options = null,
        IIssuerNameService issuerNameService = null,
        IResourceStore resourceStore = null,
        IClientStore clients = null,
        ICustomAuthorizeRequestValidator customValidator = null,
        IRedirectUriValidator uriValidator = null,
        IResourceValidator resourceValidator = null,
        IRequestObjectValidator requestObjectValidator = null)
    {
        if (options == null)
        {
            options = TestIdentityServerOptions.Create();
        }
            
        if (issuerNameService == null)
        {
            issuerNameService = new TestIssuerNameService(options.IssuerUri);
        }

        if (resourceStore == null)
        {
            resourceStore = new InMemoryResourcesStore(TestScopes.GetIdentity(), TestScopes.GetApis(), TestScopes.GetScopes());
        }

        if (clients == null)
        {
            clients = new InMemoryClientStore(TestClients.Get());
        }

        if (customValidator == null)
        {
            customValidator = new DefaultCustomAuthorizeRequestValidator();
        }

        if (uriValidator == null)
        {
            uriValidator = new StrictRedirectUriValidator(options);
        }

        if (resourceValidator == null)
        {
            resourceValidator = CreateResourceValidator(resourceStore);
        }
    
        var userSession = new MockUserSession();

        if (requestObjectValidator == null)
        {
            requestObjectValidator = CreateRequestObjectValidator();
        }
        
        return new AuthorizeRequestValidator(
            options,
            issuerNameService,
            clients,
            customValidator,
            uriValidator,
            resourceValidator,
            userSession,
            requestObjectValidator,
            new LicenseUsageTracker(new LicenseAccessor(new IdentityServerOptions(), NullLogger<LicenseAccessor>.Instance)),
            TestLogger.Create<AuthorizeRequestValidator>());
    }

    public static RequestObjectValidator CreateRequestObjectValidator(        
        JwtRequestValidator jwtRequestValidator = null,
        IJwtRequestUriHttpClient jwtRequestUriHttpClient = null,
        IPushedAuthorizationService pushedAuthorizationService = null,
        IdentityServerOptions options = null)
    {
        jwtRequestValidator ??= new JwtRequestValidator("https://identityserver",
            new LoggerFactory().CreateLogger<JwtRequestValidator>());
        jwtRequestUriHttpClient ??= new DefaultJwtRequestUriHttpClient(
            new HttpClient(new NetworkHandler(new Exception("no jwt request uri response configured"))), options,
            new LoggerFactory(), new NoneCancellationTokenProvider());
        pushedAuthorizationService ??= new TestPushedAuthorizationService();
        options ??= TestIdentityServerOptions.Create();

        return new RequestObjectValidator(
            jwtRequestValidator,
            jwtRequestUriHttpClient,
            pushedAuthorizationService,
            options,
            TestLogger.Create<RequestObjectValidator>());
    }

    public static TokenValidator CreateTokenValidator(
        IReferenceTokenStore store = null, 
        IRefreshTokenStore refreshTokenStore = null,
        IProfileService profile = null,
        IIssuerNameService issuerNameService = null,
        IdentityServerOptions options = null, 
        IClock clock = null)
    {
        options ??= TestIdentityServerOptions.Create();
        profile ??= new TestProfileService();
        store ??= CreateReferenceTokenStore();
        clock ??= new StubClock();
        refreshTokenStore ??= CreateRefreshTokenStore();
        issuerNameService ??= new TestIssuerNameService(options.IssuerUri);

        var clients = CreateClientStore();
            
        var logger = TestLogger.Create<TokenValidator>();

        var keyInfo = new SecurityKeyInfo
        {
            Key = TestCert.LoadSigningCredentials().Key,
            SigningAlgorithm = "RS256"
        };

        var validator = new TokenValidator(
            clients: clients,
            clock: clock,
            profile: profile,
            referenceTokenStore: store,
            customValidator: new DefaultCustomTokenValidator(),
            keys: new DefaultKeyMaterialService(
                new[] { new InMemoryValidationKeysStore(new[] { keyInfo }) }, 
                Enumerable.Empty<ISigningCredentialStore>(),
                new NopAutomaticKeyManagerKeyStore()
            ),
            sessionCoordinationService: new StubSessionCoordinationService(),
            logger: logger,
            options: options,
            issuerNameService: issuerNameService);

        return validator;
    }

    public static IDeviceCodeValidator CreateDeviceCodeValidator(
        IDeviceFlowCodeService service,
        IProfileService profile = null,
        IDeviceFlowThrottlingService throttlingService = null,
        IClock clock = null)
    {
        profile = profile ?? new TestProfileService();
        throttlingService = throttlingService ?? new TestDeviceFlowThrottlingService();
        clock = clock ?? new StubClock();
            
        var validator = new DeviceCodeValidator(service, profile, throttlingService, clock, TestLogger.Create<DeviceCodeValidator>());

        return validator;
    }

    public static IClientSecretValidator CreateClientSecretValidator(IClientStore clients = null, SecretParser parser = null, SecretValidator validator = null, IdentityServerOptions options = null)
    {
        options = options ?? TestIdentityServerOptions.Create();

        if (clients == null) clients = new InMemoryClientStore(TestClients.Get());

        if (parser == null)
        {
            var parsers = new List<ISecretParser>
            {
                new BasicAuthenticationSecretParser(options, TestLogger.Create<BasicAuthenticationSecretParser>()),
                new PostBodySecretParser(options, TestLogger.Create<PostBodySecretParser>())
            };

            parser = new SecretParser(parsers, TestLogger.Create<SecretParser>());
        }

        if (validator == null)
        {
            var validators = new List<ISecretValidator>
            {
                new HashedSharedSecretValidator(TestLogger.Create<HashedSharedSecretValidator>()),
                new PlainTextSharedSecretValidator(TestLogger.Create<PlainTextSharedSecretValidator>())
            };

            validator = new SecretValidator(new StubClock(), validators, TestLogger.Create<SecretValidator>());
        }

        return new ClientSecretValidator(clients, parser, validator, new TestEventService(), TestLogger.Create<ClientSecretValidator>());
    }

    public static IAuthorizationCodeStore CreateAuthorizationCodeStore()
    {
        return new DefaultAuthorizationCodeStore(new InMemoryPersistedGrantStore(),
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultAuthorizationCodeStore>());
    }
        
    public static IRefreshTokenStore CreateRefreshTokenStore()
    {
        return new DefaultRefreshTokenStore(new InMemoryPersistedGrantStore(),
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultRefreshTokenStore>());
    }
        
    public static IReferenceTokenStore CreateReferenceTokenStore()
    {
        return new DefaultReferenceTokenStore(new InMemoryPersistedGrantStore(),
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultReferenceTokenStore>());
    }

    public static IDeviceFlowCodeService CreateDeviceCodeService()
    {
        return new DefaultDeviceFlowCodeService(new InMemoryDeviceFlowStore(), new DefaultHandleGenerationService());
    }

    public static IUserConsentStore CreateUserConsentStore()
    {
        return new DefaultUserConsentStore(new InMemoryPersistedGrantStore(),
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultUserConsentStore>());
    }
}