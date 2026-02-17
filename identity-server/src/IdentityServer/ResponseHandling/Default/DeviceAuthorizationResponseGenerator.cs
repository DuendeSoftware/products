// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// The device authorization response generator
/// </summary>
/// <seealso cref="IDeviceAuthorizationResponseGenerator" />
public class DeviceAuthorizationResponseGenerator : IDeviceAuthorizationResponseGenerator
{
    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The user code service
    /// </summary>
    protected readonly IUserCodeService UserCodeService;

    /// <summary>
    /// The device flow code service
    /// </summary>
    protected readonly IDeviceFlowCodeService DeviceFlowCodeService;

    /// <summary>
    /// The clock
    /// </summary>
    protected readonly TimeProvider Clock;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceAuthorizationResponseGenerator"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="userCodeService">The user code service.</param>
    /// <param name="deviceFlowCodeService">The device flow code service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DeviceAuthorizationResponseGenerator(IdentityServerOptions options, IUserCodeService userCodeService, IDeviceFlowCodeService deviceFlowCodeService, TimeProvider clock, ILogger<DeviceAuthorizationResponseGenerator> logger)
    {
        Options = options;
        UserCodeService = userCodeService;
        DeviceFlowCodeService = deviceFlowCodeService;
        Clock = clock;
        Logger = logger;
    }

    /// <summary>
    /// Processes the response.
    /// </summary>
    /// <param name="validationResult">The validation result.</param>
    /// <param name="baseUrl">The base URL.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">validationResult or Client</exception>
    /// <exception cref="ArgumentException">Value cannot be null or whitespace. - baseUrl</exception>
    public virtual async Task<DeviceAuthorizationResponse> ProcessAsync(DeviceAuthorizationRequestValidationResult validationResult, string baseUrl)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("DeviceAuthorizationResponseGenerator.Process");

        ArgumentNullException.ThrowIfNull(validationResult);
        if (validationResult.ValidatedRequest.Client == null)
        {
            throw new ArgumentNullException(nameof(validationResult.ValidatedRequest.Client));
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(baseUrl));
        }

        Logger.LogTrace("Creating response for device authorization request");

        var response = new DeviceAuthorizationResponse();

        // generate user_code
        var userCodeGenerator = await UserCodeService.GetGenerator(
            validationResult.ValidatedRequest.Client.UserCodeType ??
            Options.DeviceFlow.DefaultUserCodeType);

        var retryCount = 0;

        while (retryCount < userCodeGenerator.RetryLimit)
        {
            var userCode = await userCodeGenerator.GenerateAsync();

            var deviceCode = await DeviceFlowCodeService.FindByUserCodeAsync(userCode);
            if (deviceCode == null)
            {
                response.UserCode = userCode;
                break;
            }

            retryCount++;
        }

        if (response.UserCode == null)
        {
            throw new InvalidOperationException("Unable to create unique device flow user code");
        }

        // generate verification URIs
        response.VerificationUri = Options.UserInteraction.DeviceVerificationUrl;
        if (response.VerificationUri.IsLocalUrl())
        {
            // if url is relative, parse absolute URL
            response.VerificationUri = baseUrl.RemoveTrailingSlash() + Options.UserInteraction.DeviceVerificationUrl;
        }

        if (!string.IsNullOrWhiteSpace(Options.UserInteraction.DeviceVerificationUserCodeParameter))
        {
            response.VerificationUriComplete =
                $"{response.VerificationUri}?{Options.UserInteraction.DeviceVerificationUserCodeParameter}={response.UserCode}";
        }

        // expiration
        response.DeviceCodeLifetime = validationResult.ValidatedRequest.Client.DeviceCodeLifetime;

        // interval
        response.Interval = Options.DeviceFlow.Interval;

        // store device request (device code & user code)
        response.DeviceCode = await DeviceFlowCodeService.StoreDeviceAuthorizationAsync(response.UserCode, new DeviceCode
        {
            Description = validationResult.ValidatedRequest.Description,
            ClientId = validationResult.ValidatedRequest.Client.ClientId,
            IsOpenId = validationResult.ValidatedRequest.IsOpenIdRequest,
            Lifetime = response.DeviceCodeLifetime,
            CreationTime = Clock.GetUtcNow().UtcDateTime,
            RequestedScopes = validationResult.ValidatedRequest.ValidatedResources.RawScopeValues
        });

        return response;
    }
}
