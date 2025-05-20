// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class RegisteredImplementationsDiagnosticEntry(IServiceProvider serviceProvider) : IDiagnosticEntry
{
    public Task WriteAsync(Utf8JsonWriter writer)
    {
        using var scope = serviceProvider.CreateScope();

        writer.WriteStartObject("RegisteredServices");

        writer.WriteStartArray("Services");

        //services
        InspectService<IBackchannelAuthenticationInteractionService>(nameof(IBackchannelAuthenticationInteractionService), writer, scope);
        InspectService<IBackchannelAuthenticationThrottlingService>(nameof(IBackchannelAuthenticationThrottlingService), writer, scope);
        InspectService<IBackchannelAuthenticationUserNotificationService>(nameof(IBackchannelAuthenticationUserNotificationService), writer, scope);
        InspectService<IBackChannelLogoutService>(nameof(IBackChannelLogoutService), writer, scope);
        InspectService(typeof(ICache<>), "ICache", writer, scope); //TODO: replace with nameof operator when C# 14 is available with support for nameof operator on open generic types
        InspectService<IClaimsService>(nameof(IClaimsService), writer, scope);
        InspectService<IConsentService>(nameof(IConsentService), writer, scope);
        InspectService<IDeviceFlowCodeService>(nameof(IDeviceFlowCodeService), writer, scope);
        InspectService<IDeviceFlowInteractionService>(nameof(IDeviceFlowCodeService), writer, scope);
        InspectService<IDeviceFlowThrottlingService>(nameof(IDeviceFlowThrottlingService), writer, scope);
        InspectService<IEventService>(nameof(IEventService), writer, scope);
        InspectService<IEventSink>(nameof(IEventSink), writer, scope);
        InspectService<IHandleGenerationService>(nameof(IHandleGenerationService), writer, scope);
        InspectService<IIdentityServerInteractionService>(nameof(IIdentityServerInteractionService), writer, scope);
        InspectService<IIssuerNameService>(nameof(IIssuerNameService), writer, scope);
        InspectService<IKeyMaterialService>(nameof(IKeyMaterialService), writer, scope);
        InspectService<ILogoutNotificationService>(nameof(ILogoutNotificationService), writer, scope);
        InspectService<IPersistedGrantService>(nameof(IPersistedGrantService), writer, scope);
        InspectService<IProfileService>(nameof(IProfileService), writer, scope);
        InspectService<IPushedAuthorizationSerializer>(nameof(IPushedAuthorizationSerializer), writer, scope);
        InspectService<IPushedAuthorizationService>(nameof(IPushedAuthorizationService), writer, scope);
        InspectService<IRefreshTokenService>(nameof(IRefreshTokenService), writer, scope);
        InspectService<IReplayCache>(nameof(IReplayCache), writer, scope);
        InspectService<IReturnUrlParser>(nameof(IReturnUrlParser), writer, scope);
        InspectService<IServerUrls>(nameof(IServerUrls), writer, scope);
        InspectService<ISessionCoordinationService>(nameof(ISessionCoordinationService), writer, scope);
        InspectService<ISessionManagementService>(nameof(ISessionManagementService), writer, scope);
        InspectService<ITokenCreationService>(nameof(ITokenCreationService), writer, scope);
        InspectService<ITokenService>(nameof(ITokenService), writer, scope);
        InspectService<IUserCodeGenerator>(nameof(IUserCodeGenerator), writer, scope);
        InspectService<IUserCodeService>(nameof(IUserCodeService), writer, scope);
        InspectService<IUserSession>(nameof(IUserSession), writer, scope);

        //stores
        InspectService<IAuthorizationCodeStore>(nameof(IAuthorizationCodeStore), writer, scope);
        InspectService<IAuthorizationParametersMessageStore>(nameof(IAuthorizationParametersMessageStore), writer, scope);
        InspectService<IClientStore>(nameof(IClientStore), writer, scope);
        InspectService<IConsentMessageStore>(nameof(IConsentMessageStore), writer, scope);
        InspectService<IDeviceFlowStore>(nameof(IDeviceFlowStore), writer, scope);
        InspectService<IIdentityProviderStore>(nameof(IIdentityProviderStore), writer, scope);
        InspectService(typeof(IMessageStore<>), "IMessageStore", writer, scope); //TODO: replace with nameof operator when C# 14 is available with support for nameof operator on open generic types
        InspectService<IPersistedGrantStore>(nameof(IPersistedGrantStore), writer, scope);
        InspectService<IPushedAuthorizationRequestStore>(nameof(IPushedAuthorizationRequestStore), writer, scope);
        InspectService<IReferenceTokenStore>(nameof(IReferenceTokenStore), writer, scope);
        InspectService<IResourceStore>(nameof(IResourceStore), writer, scope);
        InspectService<IServerSideSessionsMarker>(nameof(IServerSideSessionsMarker), writer, scope);
        InspectService<IServerSideSessionStore>(nameof(IServerSideSessionStore), writer, scope);
        InspectService<IServerSideTicketStore>(nameof(IServerSideTicketStore), writer, scope);
        InspectService<ISigningCredentialStore>(nameof(ISigningCredentialStore), writer, scope);
        InspectService<ISigningKeyStore>(nameof(ISigningKeyStore), writer, scope);
        InspectService<IUserConsentStore>(nameof(IUserConsentStore), writer, scope);
        InspectService<IValidationKeysStore>(nameof(IValidationKeysStore), writer, scope);

        writer.WriteEndArray();

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private void InspectService<T>(string serviceName, Utf8JsonWriter writer, IServiceScope scope) where T : class => InspectService(typeof(T), serviceName, writer, scope);

    private void InspectService(Type targetType, string serviceName, Utf8JsonWriter writer, IServiceScope scope)
    {
        writer.WriteStartObject();

        writer.WriteStartObject(serviceName);
        var service = scope.ServiceProvider.GetService(targetType);
        if (service != null)
        {
            var type = service.GetType();
            writer.WriteString("TypeName", type.FullName);
            writer.WriteString("Assembly", type.Assembly.GetName().Name);
            writer.WriteString("AssemblyVersion", type.Assembly.GetName().Version?.ToString());
        }
        else
        {
            writer.WriteString("TypeName", "Not Registered");
            writer.WriteString("Assembly", "Not Registered");
            writer.WriteString("AssemblyVersion", "Not Registered");
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
