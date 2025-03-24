// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default implementation of IBackchannelAuthenticationInteractionService.
/// </summary>
public class DefaultBackchannelAuthenticationInteractionService : IBackchannelAuthenticationInteractionService
{
    private readonly IBackChannelAuthenticationRequestStore _requestStore;
    private readonly IClientStore _clientStore;
    private readonly IUserSession _session;
    private readonly IResourceValidator _resourceValidator;
    private readonly IClock _systemClock;
    private readonly ILogger<DefaultBackchannelAuthenticationInteractionService> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    public DefaultBackchannelAuthenticationInteractionService(
        IBackChannelAuthenticationRequestStore requestStore,
        IClientStore clients,
        IUserSession session,
        IResourceValidator resourceValidator,
        IClock systemClock,
        ILogger<DefaultBackchannelAuthenticationInteractionService> logger
    )
    {
        _requestStore = requestStore;
        _clientStore = clients;
        _session = session;
        _resourceValidator = resourceValidator;
        _systemClock = systemClock;
        _logger = logger;
    }

    private async Task<BackchannelUserLoginRequest> CreateAsync(BackChannelAuthenticationRequest request)
    {
        if (request == null)
        {
            return null;
        }

        var client = await _clientStore.FindEnabledClientByIdAsync(request.ClientId);
        if (client == null)
        {
            return null;
        }

        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = client,
            Scopes = request.RequestedScopes,
            ResourceIndicators = request.RequestedResourceIndicators,
        });

        return new BackchannelUserLoginRequest
        {
            InternalId = request.InternalId,
            Subject = request.Subject,
            Client = client,
            ValidatedResources = validatedResources,
            RequestedResourceIndicators = request.RequestedResourceIndicators,
            AuthenticationContextReferenceClasses = request.AuthenticationContextReferenceClasses,
            BindingMessage = request.BindingMessage,
            Properties = request.Properties
        };
    }

    /// <inheritdoc/>
    public async Task<BackchannelUserLoginRequest> GetLoginRequestByInternalIdAsync(string id)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultBackchannelAuthenticationInteractionService.GetLoginRequestByInternalId");

        var request = await _requestStore.GetByInternalIdAsync(id);
        return await CreateAsync(request);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BackchannelUserLoginRequest>> GetPendingLoginRequestsForCurrentUserAsync()
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultBackchannelAuthenticationInteractionService.GetPendingLoginRequestsForCurrentUser");

        var list = new List<BackchannelUserLoginRequest>();

        var user = await _session.GetUserAsync();
        if (user != null)
        {
            _logger.LogDebug("No user present");

            var items = await _requestStore.GetLoginsForUserAsync(user.GetSubjectId());
            foreach (var item in items)
            {
                if (!item.IsComplete)
                {
                    var req = await CreateAsync(item);
                    if (req != null)
                    {
                        list.Add(req);
                    }
                }
            }
        }

        return list;
    }

    /// <inheritdoc/>
    public async Task CompleteLoginRequestAsync(CompleteBackchannelLoginRequest completionRequest)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultBackchannelAuthenticationInteractionService.CompleteLoginRequest");

        ArgumentNullException.ThrowIfNull(completionRequest);

        var request = await _requestStore.GetByInternalIdAsync(completionRequest.InternalId);
        if (request == null)
        {
            throw new InvalidOperationException("Invalid backchannel authentication request id.");
        }

        var subject = completionRequest.Subject ?? await _session.GetUserAsync();
        if (subject == null)
        {
            throw new InvalidOperationException("Invalid subject.");
        }

        if (subject.GetSubjectId() != request.Subject.GetSubjectId())
        {
            throw new InvalidOperationException($"User's subject id: '{subject.GetSubjectId()}' does not match subject id for backchannel authentication request: '{request.Subject.GetSubjectId()}'.");
        }

        var sid = (completionRequest.Subject == null) ?
            await _session.GetSessionIdAsync() :
            completionRequest.SessionId;

        if (completionRequest.ScopesValuesConsented != null)
        {
            var extra = completionRequest.ScopesValuesConsented.Except(request.RequestedScopes);
            if (extra.Any())
            {
                throw new InvalidOperationException("More scopes consented than originally requested.");
            }
        }

        var subjectClone = subject.Clone();
        if (!subject.HasClaim(x => x.Type == JwtClaimTypes.AuthenticationTime))
        {
            subjectClone.Identities.First().AddClaim(new Claim(JwtClaimTypes.AuthenticationTime, _systemClock.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer));
        }

        if (!subject.HasClaim(x => x.Type == JwtClaimTypes.IdentityProvider))
        {
            subjectClone.Identities.First().AddClaim(new Claim(JwtClaimTypes.IdentityProvider, IdentityServerConstants.LocalIdentityProvider));
        }

        request.IsComplete = true;
        request.Subject = subjectClone;
        request.SessionId = sid;
        request.AuthorizedScopes = completionRequest.ScopesValuesConsented;
        request.Description = completionRequest.Description;

        await _requestStore.UpdateByInternalIdAsync(completionRequest.InternalId, request);

        _logger.LogDebug("Successful update for backchannel authentication request id {id}", completionRequest.InternalId);
    }
}
