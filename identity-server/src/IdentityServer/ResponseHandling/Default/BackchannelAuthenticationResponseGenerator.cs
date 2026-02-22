// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// The backchannel authentication response generator
/// </summary>
/// <seealso cref="IBackchannelAuthenticationResponseGenerator" />
public class BackchannelAuthenticationResponseGenerator : IBackchannelAuthenticationResponseGenerator
{
    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The request store.
    /// </summary>
    protected readonly IBackChannelAuthenticationRequestStore BackChannelAuthenticationRequestStore;

    /// <summary>
    /// The user login service.
    /// </summary>
    protected readonly IBackchannelAuthenticationUserNotificationService UserLoginService;

    /// <summary>
    /// The time provider
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackchannelAuthenticationResponseGenerator"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="backChannelAuthenticationRequestStore"></param>
    /// <param name="userLoginService"></param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public BackchannelAuthenticationResponseGenerator(IdentityServerOptions options,
        IBackChannelAuthenticationRequestStore backChannelAuthenticationRequestStore,
        IBackchannelAuthenticationUserNotificationService userLoginService,
        TimeProvider timeProvider,
        ILogger<BackchannelAuthenticationResponseGenerator> logger)
    {
        Options = options;
        BackChannelAuthenticationRequestStore = backChannelAuthenticationRequestStore;
        UserLoginService = userLoginService;
        TimeProvider = timeProvider;
        Logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<BackchannelAuthenticationResponse> ProcessAsync(BackchannelAuthenticationRequestValidationResult validationResult, Ct ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("BackchannelAuthenticationResponseGenerator.Process");

        ArgumentNullException.ThrowIfNull(validationResult);
        if (validationResult.ValidatedRequest == null)
        {
            throw new ArgumentNullException(nameof(validationResult.ValidatedRequest));
        }

        if (validationResult.ValidatedRequest.Client == null)
        {
            throw new ArgumentNullException(nameof(validationResult.ValidatedRequest.Client));
        }

        Logger.LogTrace("Creating response for backchannel authentication request");

        var request = new BackChannelAuthenticationRequest
        {
            CreationTime = TimeProvider.GetUtcNow().UtcDateTime,
            ClientId = validationResult.ValidatedRequest.ClientId,
            RequestedScopes = validationResult.ValidatedRequest.ValidatedResources.RawScopeValues,
            RequestedResourceIndicators = validationResult.ValidatedRequest.RequestedResourceIndicators,
            Subject = validationResult.ValidatedRequest.Subject,
            Lifetime = validationResult.ValidatedRequest.Expiry,
            AuthenticationContextReferenceClasses = validationResult.ValidatedRequest.AuthenticationContextReferenceClasses,
            Tenant = validationResult.ValidatedRequest.Tenant,
            IdP = validationResult.ValidatedRequest.IdP,
            BindingMessage = validationResult.ValidatedRequest.BindingMessage,
            Properties = validationResult.ValidatedRequest.Properties,
        };

        var requestId = await BackChannelAuthenticationRequestStore.CreateRequestAsync(request, ct);

        var interval = validationResult.ValidatedRequest.Client.PollingInterval ?? Options.Ciba.DefaultPollingInterval;
        var response = new BackchannelAuthenticationResponse()
        {
            AuthenticationRequestId = requestId,
            ExpiresIn = request.Lifetime,
            Interval = interval,
        };

        await UserLoginService.SendLoginRequestAsync(new BackchannelUserLoginRequest
        {
            InternalId = request.InternalId,
            Subject = validationResult.ValidatedRequest.Subject,
            Client = validationResult.ValidatedRequest.Client,
            ValidatedResources = validationResult.ValidatedRequest.ValidatedResources,
            RequestedResourceIndicators = validationResult.ValidatedRequest.RequestedResourceIndicators,
            BindingMessage = validationResult.ValidatedRequest.BindingMessage,
            AuthenticationContextReferenceClasses = validationResult.ValidatedRequest.AuthenticationContextReferenceClasses,
            Tenant = validationResult.ValidatedRequest.Tenant,
            IdP = validationResult.ValidatedRequest.IdP,
            Properties = validationResult.ValidatedRequest.Properties,
        }, ct);

        return response;
    }
}
