// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.Default;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Empty;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class RegisteredImplementationsDiagnosticEntry(ServiceCollectionAccessor serviceCollectionAccessor)
    : IDiagnosticEntry
{
    private readonly Dictionary<string, IEnumerable<RegisteredImplementationDetails>> _typesToInspect = new()
    {
        {
            "Root", [new (typeof(IIdentityServerTools), [typeof(IdentityServerTools)])]
        },
        {
            "Hosting", [
                new (typeof(IEndpointHandler), []),
                new (typeof(IEndpointResult), []),
                new (typeof(IEndpointRouter), [typeof(EndpointRouter)]),
                new (typeof(IHttpResponseWriter<>), []),
            ]
        },
        {
            "Infrastructure", [
                new(typeof(IConcurrencyLock<>), [typeof(DefaultConcurrencyLock<>)]),
            ]
        },
        {
            "ResponseHandling", [
                new(typeof(IAuthorizeInteractionResponseGenerator), [typeof(AuthorizeInteractionResponseGenerator)]),
                new(typeof(IAuthorizeResponseGenerator), [typeof(AuthorizeResponseGenerator)]),
                new(typeof(IBackchannelAuthenticationResponseGenerator), [typeof(BackchannelAuthenticationResponseGenerator)]),
                new(typeof(IDeviceAuthorizationResponseGenerator), [typeof(DeviceAuthorizationResponseGenerator)]),
                new(typeof(IDiscoveryResponseGenerator), [typeof(DiscoveryResponseGenerator)]),
                new(typeof(IIntrospectionResponseGenerator), [typeof(IntrospectionResponseGenerator)]),
                new(typeof(IPushedAuthorizationResponseGenerator), [typeof(PushedAuthorizationResponseGenerator)]),
                new(typeof(ITokenResponseGenerator), [typeof(TokenResponseGenerator)]),
                new(typeof(ITokenRevocationResponseGenerator), [typeof(TokenRevocationResponseGenerator)]),
                new(typeof(IUserInfoResponseGenerator), [typeof(UserInfoResponseGenerator)]),
            ]
        },
        {
            "SAML", [
                new(typeof(ISamlClaimsMapper), []),
                new(typeof(ISamlFrontChannelLogout), [typeof(SamlHttpPostFrontChannelLogout), typeof(SamlHttpRedirectFrontChannelLogout)]),
                new(typeof(ISamlInteractionService),[]),
                new(typeof(ISamlLogoutNotificationService), [typeof(NopSamlLogoutNotificationService)]),
                new(typeof(ISamlSigninInteractionResponseGenerator),[]),
            ]
        },
        {
            "Services", [
                new(typeof(IAutomaticKeyManagerKeyStore), [typeof(AutomaticKeyManagerKeyStore)]),
                new(typeof(IBackchannelAuthenticationInteractionService), [typeof(DefaultBackchannelAuthenticationInteractionService)]),
                new(typeof(IBackchannelAuthenticationThrottlingService), [typeof(DistributedBackchannelAuthenticationThrottlingService)]),
                new(typeof(IBackchannelAuthenticationUserNotificationService), [typeof(NopBackchannelAuthenticationUserNotificationService)]),
                new(typeof(IBackChannelLogoutHttpClient), [typeof(DefaultBackChannelLogoutHttpClient)]),
                new(typeof(IBackChannelLogoutService), [typeof(DefaultBackChannelLogoutService)]),
                new(typeof(ICache<>), [typeof(DefaultCache<>)]),
                new(typeof(IClaimsService), [typeof(DefaultClaimsService)]),
                new(typeof(IConsentService), [typeof(DefaultConsentService)]),
                new(typeof(ICorsPolicyService), [typeof(DefaultCorsPolicyService)]),
                new(typeof(IDeviceFlowCodeService), [typeof(DefaultDeviceFlowCodeService)]),
                new(typeof(IDeviceFlowInteractionService), [typeof(DefaultDeviceFlowInteractionService)]),
                new(typeof(IDeviceFlowThrottlingService), [typeof(DistributedDeviceFlowThrottlingService)]),
                new(typeof(IEventService), [typeof(DefaultEventService)]),
                new(typeof(IEventSink), [typeof(DefaultEventSink)]),
                new(typeof(IHandleGenerationService), [typeof(DefaultHandleGenerationService)]),
                new(typeof(IIdentityServerInteractionService), [typeof(DefaultIdentityServerInteractionService)]),
                new(typeof(IIssuerNameService), [typeof(DefaultIssuerNameService)]),
                new(typeof(IJwtRequestUriHttpClient), [typeof(DefaultJwtRequestUriHttpClient)]),
                new(typeof(IKeyManager), [typeof(KeyManager)]),
                new(typeof(IKeyMaterialService), [typeof(DefaultKeyMaterialService)]),
                new(typeof(ILogoutNotificationService), [typeof(LogoutNotificationService)]),
                new(typeof(IPersistedGrantService), [typeof(DefaultPersistedGrantService)]),
                new(typeof(IProfileService), [typeof(DefaultProfileService)]),
                new(typeof(IPushedAuthorizationSerializer), [typeof(PushedAuthorizationSerializer)]),
                new(typeof(IPushedAuthorizationService), [typeof(PushedAuthorizationService)]),
                new(typeof(IRefreshTokenService), [typeof(DefaultRefreshTokenService)]),
                new(typeof(IReplayCache), [typeof(DefaultReplayCache)]),
                new(typeof(IReturnUrlParser), [typeof(OidcReturnUrlParser)]),
                new(typeof(IServerUrls), [typeof(DefaultServerUrls)]),
                new(typeof(ISessionCoordinationService), [typeof(DefaultSessionCoordinationService)]),
                new(typeof(ISessionManagementService), []),
                new(typeof(ISigningKeyProtector), [typeof(DataProtectionKeyProtector)]),
                new(typeof(ISigningKeyStoreCache), [typeof(InMemoryKeyStoreCache)]),
                new(typeof(ITokenCreationService), [typeof(DefaultTokenCreationService)]),
                new(typeof(ITokenService), [typeof(DefaultTokenService)]),
                new(typeof(IUserCodeGenerator), [typeof(NumericUserCodeGenerator)]),
                new(typeof(IUserCodeService), [typeof(DefaultUserCodeService)]),
                new(typeof(IUserSession), [typeof(DefaultUserSession)]),
                new(typeof(IUiLocalesService), [typeof(DefaultUiLocalesService)]),
            ]
        },
        {
            "Stores", [
                new(typeof(IAuthorizationCodeStore), [typeof(DefaultAuthorizationCodeStore)]),
                new(typeof(IAuthorizationParametersMessageStore), []),
                new(typeof(IBackChannelAuthenticationRequestStore), [typeof(DefaultBackChannelAuthenticationRequestStore)]),
                new(typeof(IClientStore), [typeof(EmptyClientStore)]),
                new(typeof(IConsentMessageStore), [typeof(ConsentMessageStore)]),
                new(typeof(IDeviceFlowStore), []),
                new(typeof(IIdentityProviderStore), [typeof(NopIdentityProviderStore)]),
                new(typeof(IMessageStore<>), [typeof(ProtectedDataMessageStore<>)]),
                new(typeof(IPersistentGrantSerializer), [typeof(PersistentGrantSerializer)]),
                new(typeof(IPersistedGrantStore), [typeof(InMemoryPersistedGrantStore)]),
                new(typeof(IPushedAuthorizationRequestStore), [typeof(InMemoryPushedAuthorizationRequestStore)]),
                new(typeof(IReferenceTokenStore), [typeof(DefaultReferenceTokenStore)]),
                new(typeof(IRefreshTokenStore), [typeof(DefaultRefreshTokenStore)]),
                new(typeof(IResourceStore), [typeof(EmptyResourceStore)]),
                new(typeof(ISamlServiceProviderStore), [typeof(EmptySamlServiceProviderStore)]),
                new(typeof(IServerSideSessionsMarker), []),
                new(typeof(IServerSideSessionStore),[]),
                new(typeof(IServerSideTicketStore),[]),
                new(typeof(ISigningCredentialStore), []),
                new(typeof(ISigningKeyStore), [typeof(FileSystemKeyStore)]),
                new(typeof(IUserConsentStore), [typeof(DefaultUserConsentStore)]),
                new(typeof(IValidationKeysStore), []),
            ]
        },
        {
            "Validation", [
                new(typeof(IApiSecretValidator),[typeof(ApiSecretValidator)]),
                new(typeof(IAuthorizeRequestValidator), [typeof(AuthorizeRequestValidator)]),
                new(typeof(IBackchannelAuthenticationRequestIdValidator), [typeof(BackchannelAuthenticationRequestIdValidator)]),
                new(typeof(IBackchannelAuthenticationRequestValidator), [typeof(BackchannelAuthenticationRequestValidator)]),
                new(typeof(IBackchannelAuthenticationUserValidator), [typeof(NopBackchannelAuthenticationUserValidator)]),
                new(typeof(IClientConfigurationValidator), [typeof(DefaultClientConfigurationValidator)]),
                new(typeof(IClientSecretValidator), [typeof(ClientSecretValidator)]),
                new(typeof(ICustomAuthorizeRequestValidator), [typeof(DefaultCustomAuthorizeRequestValidator)]),
                new(typeof(ICustomBackchannelAuthenticationValidator), [typeof(DefaultCustomBackchannelAuthenticationValidator)]),
                new(typeof(ICustomTokenRequestValidator), [typeof(DefaultCustomTokenRequestValidator)]),
                new(typeof(ICustomTokenValidator), [typeof(DefaultCustomTokenValidator)]),
                new(typeof(IDeviceAuthorizationRequestValidator), [typeof(DeviceAuthorizationRequestValidator)]),
                new(typeof(IDeviceCodeValidator), [typeof(DeviceCodeValidator)]),
                new(typeof(IDPoPProofValidator), [typeof(DefaultDPoPProofValidator)]),
                new(typeof(IEndSessionRequestValidator), [typeof(EndSessionRequestValidator)]),
                new(typeof(IExtensionGrantValidator), []),
                new(typeof(IIdentityProviderConfigurationValidator), [typeof(DefaultIdentityProviderConfigurationValidator)]),
                new(typeof(IIntrospectionRequestValidator), [typeof(IntrospectionRequestValidator)]),
                new(typeof(IIssuerPathValidator), [typeof(DefaultIssuerPathValidator)]),
                new(typeof(IJwtRequestValidator), [typeof(JwtRequestValidator)]),
                new(typeof(IPushedAuthorizationRequestValidator), [typeof(PushedAuthorizationRequestValidator)]),
                new(typeof(IRedirectUriValidator), [typeof(StrictRedirectUriValidator)]),
                new(typeof(IResourceOwnerPasswordValidator), [typeof(NotSupportedResourceOwnerPasswordValidator)]),
                new(typeof(IResourceValidator), [typeof(DefaultResourceValidator)]),
                new(typeof(IScopeParser), [typeof(DefaultScopeParser)]),
                new(typeof(ISecretParser), [typeof(BasicAuthenticationSecretParser), typeof(PostBodySecretParser)]),
                new(typeof(ISecretsListParser), [typeof(SecretParser)]),
                new(typeof(ISecretsListValidator), [typeof(SecretValidator)]),
                new(typeof(ISecretValidator), [typeof(HashedSharedSecretValidator)]),
                new(typeof(ITokenRequestValidator),[typeof(TokenRequestValidator)]),
                new(typeof(ITokenRevocationRequestValidator), [typeof(TokenRevocationRequestValidator)]),
                new(typeof(ITokenValidator), [typeof(TokenValidator)]),
                new(typeof(IUserInfoRequestValidator), [typeof(UserInfoRequestValidator)]),
            ]
        }
    };

    public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
    {
        writer.WriteStartObject("RegisteredImplementations");

        foreach (var group in _typesToInspect)
        {
            writer.WriteStartArray(group.Key);

            foreach (var implementationInfo in group.Value)
            {
                WriteImplementationDetails(implementationInfo, writer);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private void WriteImplementationDetails(RegisteredImplementationDetails registeredImplementationDetails, Utf8JsonWriter writer)
    {
        var services = serviceCollectionAccessor.ServiceCollection.Where(descriptor =>
            descriptor.ServiceType == registeredImplementationDetails.TInterface &&
            descriptor.ImplementationType != null &&
            !registeredImplementationDetails.TDefaultImplementations.Contains(descriptor.ImplementationType));

        if (!services.Any())
        {
            return;
        }

        writer.WriteStartObject();
        writer.WriteStartArray(registeredImplementationDetails.TInterface.Name);

        foreach (var service in services)
        {
            var type = service.ImplementationType!;
            writer.WriteStartObject();
            writer.WriteString("TypeName", type.FullName);
            writer.WriteString("Assembly", type.Assembly.GetName().Name);
            writer.WriteString("AssemblyVersion", type.Assembly.GetName().Version?.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
