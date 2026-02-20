// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

internal class DefaultDeviceFlowInteractionService : IDeviceFlowInteractionService
{
    private readonly IClientStore _clients;
    private readonly IUserSession _session;
    private readonly IDeviceFlowCodeService _devices;
    private readonly IResourceValidator _resourceValidator;
    private readonly ILogger<DefaultDeviceFlowInteractionService> _logger;

    public DefaultDeviceFlowInteractionService(
        IClientStore clients,
        IUserSession session,
        IDeviceFlowCodeService devices,
        IResourceValidator resourceValidator,
        ILogger<DefaultDeviceFlowInteractionService> logger)
    {
        _clients = clients;
        _session = session;
        _devices = devices;
        _resourceValidator = resourceValidator;
        _logger = logger;
    }

    public async Task<DeviceFlowAuthorizationRequest> GetAuthorizationContextAsync(string userCode, CT ct)
    {
        var deviceAuth = await _devices.FindByUserCodeAsync(userCode);
        if (deviceAuth == null)
        {
            return null;
        }

        var client = await _clients.FindEnabledClientByIdAsync(deviceAuth.ClientId, ct);
        if (client == null)
        {
            return null;
        }

        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = client,
            Scopes = deviceAuth.RequestedScopes,
        });

        return new DeviceFlowAuthorizationRequest
        {
            Client = client,
            ValidatedResources = validatedResources
        };
    }

    public async Task<DeviceFlowInteractionResult> HandleRequestAsync(string userCode, ConsentResponse consent, CT ct)
    {
        ArgumentNullException.ThrowIfNull(userCode);
        ArgumentNullException.ThrowIfNull(consent);

        var deviceAuth = await _devices.FindByUserCodeAsync(userCode);
        if (deviceAuth == null)
        {
            return LogAndReturnError("Invalid user code", "Device authorization failure - user code is invalid");
        }

        var client = await _clients.FindEnabledClientByIdAsync(deviceAuth.ClientId, ct);
        if (client == null)
        {
            return LogAndReturnError("Invalid client", "Device authorization failure - requesting client is invalid");
        }

        var subject = await _session.GetUserAsync();
        if (subject == null)
        {
            return LogAndReturnError("No user present in device flow request", "Device authorization failure - no user found");
        }

        var sid = await _session.GetSessionIdAsync();

        deviceAuth.IsAuthorized = true;
        deviceAuth.Subject = subject;
        deviceAuth.SessionId = sid;
        deviceAuth.Description = consent.Description;
        deviceAuth.AuthorizedScopes = consent.ScopesValuesConsented;

        // TODO: Device Flow - Record consent template
        if (consent.RememberConsent)
        {
            //var consentRequest = new ConsentRequest(request, subject);
            //await _consentMessageStore.WriteAsync(consentRequest.Id, new Message<ConsentResponse>(consent, _clock.UtcNow.UtcDateTime));
        }

        await _devices.UpdateByUserCodeAsync(userCode, deviceAuth);

        return new DeviceFlowInteractionResult();
    }

    private DeviceFlowInteractionResult LogAndReturnError(string error, string errorDescription = null)
    {
#pragma warning disable CA2254 // Structured logging is not needed for this message
        _logger.LogError(errorDescription);
#pragma warning restore CA2254
        return DeviceFlowInteractionResult.Failure(error);
    }
}
