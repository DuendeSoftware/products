// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default implementation of IBackchannelAuthenticationRequestIdValidator.
/// </summary>
internal class BackchannelAuthenticationRequestIdValidator : IBackchannelAuthenticationRequestIdValidator
{
    private readonly IBackChannelAuthenticationRequestStore _backchannelAuthenticationStore;
    private readonly IProfileService _profile;
    private readonly IBackchannelAuthenticationThrottlingService _throttlingService;
    private readonly IClock _systemClock;
    private readonly ILogger<BackchannelAuthenticationRequestIdValidator> _logger;

    public BackchannelAuthenticationRequestIdValidator(
        IBackChannelAuthenticationRequestStore backchannelAuthenticationStore,
        IProfileService profile,
        IBackchannelAuthenticationThrottlingService throttlingService,
        IClock systemClock,
        ILogger<BackchannelAuthenticationRequestIdValidator> logger)
    {
        _backchannelAuthenticationStore = backchannelAuthenticationStore;
        _profile = profile;
        _throttlingService = throttlingService;
        _systemClock = systemClock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ValidateAsync(BackchannelAuthenticationRequestIdValidationContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("BackchannelAuthenticationRequestIdValidator.Validate");

        var request = await _backchannelAuthenticationStore.GetByAuthenticationRequestIdAsync(context.AuthenticationRequestId);

        if (request == null)
        {
            _logger.LogError("Invalid authentication request id");
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.InvalidGrant);
            return;
        }

        // validate client binding
        if (request.ClientId != context.Request.Client.ClientId)
        {
            _logger.LogError("Client {ClientId} is trying to use a authentication request id from client {RequestClientId}", context.Request.Client.ClientId, request.ClientId);
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.InvalidGrant);
            return;
        }

        if (await _throttlingService.ShouldSlowDown(context.AuthenticationRequestId, request))
        {
            _logger.LogError("Client {ClientId} is polling too fast", request.ClientId);
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.SlowDown);
            return;
        }

        // validate lifetime
        if (request.CreationTime.AddSeconds(request.Lifetime) < _systemClock.UtcNow.UtcDateTime)
        {
            _logger.LogError("Expired authentication request id");
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.ExpiredToken);
            return;
        }

        // denied
        if (request.IsComplete
            && (request.AuthorizedScopes == null || !request.AuthorizedScopes.Any()))
        {
            _logger.LogError("No scopes authorized for backchannel authentication request. Access denied");
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.AccessDenied);
            await _backchannelAuthenticationStore.RemoveByInternalIdAsync(request.InternalId);
            return;
        }

        // make sure authentication request id is complete
        if (!request.IsComplete)
        {
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.AuthorizationPending);
            return;
        }

        // make sure user is enabled
        var isActiveCtx = new IsActiveContext(request.Subject, context.Request.Client, IdentityServerConstants.ProfileIsActiveCallers.BackchannelAuthenticationRequestIdValidation);
        await _profile.IsActiveAsync(isActiveCtx);

        if (!isActiveCtx.IsActive)
        {
            _logger.LogError("User has been disabled: {subjectId}", request.Subject.GetSubjectId());
            context.Result = new TokenRequestValidationResult(context.Request, OidcConstants.TokenErrors.InvalidGrant);
            return;
        }

        context.Request.BackChannelAuthenticationRequest = request;
        context.Request.Subject = request.Subject;
        context.Request.SessionId = request.SessionId;

        context.Result = new TokenRequestValidationResult(context.Request);

        await _backchannelAuthenticationStore.RemoveByInternalIdAsync(request.InternalId);

        _logger.LogDebug("Success validating backchannel authentication request id.");
    }
}
