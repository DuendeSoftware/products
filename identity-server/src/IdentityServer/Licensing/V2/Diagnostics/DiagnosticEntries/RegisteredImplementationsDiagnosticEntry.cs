// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class RegisteredImplementationsDiagnosticEntry(ServiceCollectionAccessor serviceCollectionAccessor)
    : IDiagnosticEntry
{
    private readonly Dictionary<string, IEnumerable<Type>> _typesToInspect = new()
    {
        {
            "Root", [ typeof(IIdentityServerTools) ]
        },
        {
            "Hosting", [
                typeof(IEndpointHandler),
                typeof(IEndpointResult),
                typeof(IEndpointRouter),
                typeof(IHttpResponseWriter<>)
            ]
        },
        {
            "Infrastructure", [typeof(IClock), typeof(IConcurrencyLock<>)]
        },
        {
            "ResponseHandling", [
                typeof(IAuthorizeInteractionResponseGenerator),
                typeof(IAuthorizeResponseGenerator),
                typeof(IBackchannelAuthenticationResponseGenerator),
                typeof(IDeviceAuthorizationResponseGenerator),
                typeof(IDiscoveryResponseGenerator),
                typeof(IIntrospectionResponseGenerator),
                typeof(IPushedAuthorizationResponseGenerator),
                typeof(ITokenResponseGenerator),
                typeof(ITokenRevocationResponseGenerator),
                typeof(IUserInfoResponseGenerator)
            ]
        },
        {
            "Services", [
                typeof(IAutomaticKeyManagerKeyStore),
                typeof(IBackchannelAuthenticationInteractionService),
                typeof(IBackchannelAuthenticationThrottlingService),
                typeof(IBackchannelAuthenticationUserNotificationService),
                typeof(IBackChannelLogoutHttpClient),
                typeof(IBackChannelLogoutService),
                typeof(ICache<>),
                typeof(ICancellationTokenProvider),
                typeof(IClaimsService),
                typeof(IConsentService),
                typeof(ICorsPolicyService),
                typeof(IDeviceFlowCodeService),
                typeof(IDeviceFlowInteractionService),
                typeof(IDeviceFlowThrottlingService),
                typeof(IEventService),
                typeof(IEventSink),
                typeof(IHandleGenerationService),
                typeof(IIdentityServerInteractionService),
                typeof(IIssuerNameService),
                typeof(IJwtRequestUriHttpClient),
                typeof(IKeyManager),
                typeof(IKeyMaterialService),
                typeof(ILogoutNotificationService),
                typeof(IPersistedGrantService),
                typeof(IProfileService),
                typeof(IPushedAuthorizationSerializer),
                typeof(IPushedAuthorizationService),
                typeof(IRefreshTokenService),
                typeof(IReplayCache),
                typeof(IReturnUrlParser),
                typeof(IServerUrls),
                typeof(ISessionCoordinationService),
                typeof(ISessionManagementService),
                typeof(ISigningKeyProtector),
                typeof(ISigningKeyStoreCache),
                typeof(ITokenCreationService),
                typeof(ITokenService),
                typeof(IUserCodeGenerator),
                typeof(IUserCodeService),
                typeof(IUserSession)
            ]
        },
        {
            "Stores", [
                typeof(IAuthorizationCodeStore),
                typeof(IAuthorizationParametersMessageStore),
                typeof(IBackChannelAuthenticationRequestStore),
                typeof(IClientStore),
                typeof(IConsentMessageStore),
                typeof(IDeviceFlowStore),
                typeof(IIdentityProviderStore),
                typeof(IMessageStore<>),
                typeof(IPersistentGrantSerializer),
                typeof(IPersistedGrantStore),
                typeof(IPushedAuthorizationRequestStore),
                typeof(IReferenceTokenStore),
                typeof(IRefreshTokenStore),
                typeof(IResourceStore),
                typeof(IServerSideSessionsMarker),
                typeof(IServerSideSessionStore),
                typeof(IServerSideTicketStore),
                typeof(ISigningCredentialStore),
                typeof(ISigningKeyStore),
                typeof(IUserConsentStore),
                typeof(IValidationKeysStore)
            ]
        },
        {
            "Validation", [
                typeof(IApiSecretValidator),
                typeof(IAuthorizeRequestValidator),
                typeof(IBackchannelAuthenticationRequestIdValidator),
                typeof(IBackchannelAuthenticationRequestValidator),
                typeof(IBackchannelAuthenticationUserValidator),
                typeof(IClientConfigurationValidator),
                typeof(IClientSecretValidator),
                typeof(ICustomAuthorizeRequestValidator),
                typeof(ICustomBackchannelAuthenticationValidator),
                typeof(ICustomTokenRequestValidator),
                typeof(ICustomTokenValidator),
                typeof(IDeviceAuthorizationRequestValidator),
                typeof(IDeviceCodeValidator),
                typeof(IDPoPProofValidator),
                typeof(IEndSessionRequestValidator),
                typeof(IExtensionGrantValidator),
                typeof(IIdentityProviderConfigurationValidator),
                typeof(IIntrospectionRequestValidator),
                typeof(IJwtRequestValidator),
                typeof(IPushedAuthorizationRequestValidator),
                typeof(IRedirectUriValidator),
                typeof(IResourceOwnerPasswordValidator),
                typeof(IResourceValidator),
                typeof(IScopeParser),
                typeof(ISecretParser),
                typeof(ISecretsListParser),
                typeof(ISecretsListValidator),
                typeof(ISecretValidator),
                typeof(ITokenRequestValidator),
                typeof(ITokenRevocationRequestValidator),
                typeof(ITokenValidator),
                typeof(IUserInfoRequestValidator)
            ]
        }
    };

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("RegisteredImplementations");

        foreach (var group in _typesToInspect)
        {
            writer.WriteStartArray(group.Key);

            foreach (var type in group.Value)
            {
                WriteImplementationDetails(type, type.Name, writer);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private void WriteImplementationDetails(Type targetType, string serviceName, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartArray(serviceName);

        var services = serviceCollectionAccessor.ServiceCollection.Where(descriptor =>
            descriptor.ServiceType == targetType &&
            descriptor.ImplementationType != null);
        if (services.Any())
        {
            foreach (var service in services)
            {
                var type = service.ImplementationType!;
                writer.WriteStartObject();
                writer.WriteString("TypeName", type.FullName);
                writer.WriteString("Assembly", type.Assembly.GetName().Name);
                writer.WriteString("AssemblyVersion", type.Assembly.GetName().Version?.ToString());
                writer.WriteEndObject();
            }
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString("TypeName", "Not Registered");
            writer.WriteString("Assembly", "Not Registered");
            writer.WriteString("AssemblyVersion", "Not Registered");
            writer.WriteEndObject();
        }


        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
