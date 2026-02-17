// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates an incoming token request using the device flow
/// </summary>
internal class DeviceCodeValidator : IDeviceCodeValidator
{
    private readonly IDeviceFlowCodeService _devices;
    private readonly IProfileService _profile;
    private readonly IDeviceFlowThrottlingService _throttlingService;
    private readonly TimeProvider _systemClock;
    private readonly ILogger<DeviceCodeValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceCodeValidator"/> class.
    /// </summary>
    /// <param name="devices">The devices.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="throttlingService">The throttling service.</param>
    /// <param name="systemClock">The system clock.</param>
    /// <param name="logger">The logger.</param>
    public DeviceCodeValidator(
        IDeviceFlowCodeService devices,
        IProfileService profile,
        IDeviceFlowThrottlingService throttlingService,
        TimeProvider systemClock,
        ILogger<DeviceCodeValidator> logger)
    {
        _devices = devices;
        _profile = profile;
        _throttlingService = throttlingService;
        _systemClock = systemClock;
        _logger = logger;
    }

    /// <summary>
    /// Validates the device code.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    public async Task ValidateAsync(DeviceCodeValidationContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("DeviceCodeValidator.Validate");

        var deviceCode = await _devices.FindByDeviceCodeAsync(context.DeviceCode);

        if (deviceCode == null)
        {
            _logger.LogError("Invalid device code");
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.InvalidGrant);
            return;
        }

        // validate client binding
        if (deviceCode.ClientId != context.Request.Client.ClientId)
        {
            _logger.LogError("Client {ClientId} is trying to use a device code from client {DeviceCodeClientId}", context.Request.Client.ClientId, deviceCode.ClientId);
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.InvalidGrant);
            return;
        }

        if (await _throttlingService.ShouldSlowDown(context.DeviceCode, deviceCode))
        {
            _logger.LogError("Client {ClientId} is polling too fast", deviceCode.ClientId);
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.SlowDown);
            return;
        }

        // validate lifetime
        if (deviceCode.CreationTime.AddSeconds(deviceCode.Lifetime) < _systemClock.GetUtcNow().UtcDateTime)
        {
            _logger.LogError("Expired device code");
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.ExpiredToken);
            return;
        }

        // denied
        if (deviceCode.IsAuthorized
            && (deviceCode.AuthorizedScopes == null || deviceCode.AuthorizedScopes.Any() == false))
        {
            _logger.LogError("No scopes authorized for device authorization. Access denied");
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.AccessDenied);
            return;
        }

        // make sure code is authorized
        if (!deviceCode.IsAuthorized || deviceCode.Subject == null)
        {
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.AuthorizationPending);
            return;
        }

        // make sure user is enabled
        var isActiveCtx = new IsActiveContext(deviceCode.Subject, context.Request.Client, IdentityServerConstants.ProfileIsActiveCallers.DeviceCodeValidation);
        await _profile.IsActiveAsync(isActiveCtx);

        if (isActiveCtx.IsActive == false)
        {
            _logger.LogError("User has been disabled: {subjectId}", deviceCode.Subject.GetSubjectId());
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.InvalidGrant);
            return;
        }

        context.Request.DeviceCode = deviceCode;
        context.Request.SessionId = deviceCode.SessionId;

        context.Result = new TokenRequestValidationResult(context.Request);
        await _devices.RemoveByDeviceCodeAsync(context.DeviceCode);
    }
}
