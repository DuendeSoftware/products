// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Logging;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

internal class DeviceAuthorizationRequestValidator : IDeviceAuthorizationRequestValidator
{
    private readonly IdentityServerOptions _options;
    private readonly IResourceValidator _resourceValidator;
    private readonly ILogger<DeviceAuthorizationRequestValidator> _logger;

    public DeviceAuthorizationRequestValidator(
        IdentityServerOptions options,
        IResourceValidator resourceValidator,
        ILogger<DeviceAuthorizationRequestValidator> logger)
    {
        _options = options;
        _resourceValidator = resourceValidator;
        _logger = logger;
    }

    public async Task<DeviceAuthorizationRequestValidationResult> ValidateAsync(NameValueCollection parameters, ClientSecretValidationResult clientValidationResult, CT ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("DeviceAuthorizationRequestValidator.Validate");

        _logger.LogDebug("Start device authorization request validation");

        var request = new ValidatedDeviceAuthorizationRequest
        {
            Raw = parameters ?? throw new ArgumentNullException(nameof(parameters)),
            Options = _options
        };

        var clientResult = ValidateClient(request, clientValidationResult);
        if (clientResult.IsError)
        {
            return clientResult;
        }

        var scopeResult = await ValidateScopeAsync(request, ct);
        if (scopeResult.IsError)
        {
            return scopeResult;
        }

        _logger.LogDebug("{clientId} device authorization request validation success", request.Client.ClientId);
        return Valid(request);
    }

    private static DeviceAuthorizationRequestValidationResult Valid(ValidatedDeviceAuthorizationRequest request) => new DeviceAuthorizationRequestValidationResult(request);

    private static DeviceAuthorizationRequestValidationResult Invalid(ValidatedDeviceAuthorizationRequest request, string error = OidcConstants.AuthorizeErrors.InvalidRequest, string description = null) => new DeviceAuthorizationRequestValidationResult(request, error, description);

    private void LogError(string message, ValidatedDeviceAuthorizationRequest request)
    {
        var requestDetails = new DeviceAuthorizationRequestValidationLog(request);
        _logger.LogError("{Message}:{RequestDetails}", message, requestDetails);
    }

    private void LogError(string message, string detail, ValidatedDeviceAuthorizationRequest request)
    {
        var requestDetails = new DeviceAuthorizationRequestValidationLog(request);
        _logger.LogError("{Message}: {Detail}:{RequestDetails}", message, detail, requestDetails);
    }

    private DeviceAuthorizationRequestValidationResult ValidateClient(ValidatedDeviceAuthorizationRequest request, ClientSecretValidationResult clientValidationResult)
    {
        //////////////////////////////////////////////////////////
        // set client & secret
        //////////////////////////////////////////////////////////
        ArgumentNullException.ThrowIfNull(clientValidationResult);
        request.SetClient(clientValidationResult.Client, clientValidationResult.Secret);

        //////////////////////////////////////////////////////////
        // check if client protocol type is oidc
        //////////////////////////////////////////////////////////
        if (request.Client.ProtocolType != IdentityServerConstants.ProtocolTypes.OpenIdConnect)
        {
            LogError("Invalid protocol type for OIDC authorize endpoint", request.Client.ProtocolType, request);
            return Invalid(request, OidcConstants.AuthorizeErrors.UnauthorizedClient, "Invalid protocol");
        }

        //////////////////////////////////////////////////////////
        // check if client allows device flow
        //////////////////////////////////////////////////////////
        if (!request.Client.AllowedGrantTypes.Contains(GrantType.DeviceFlow))
        {
            LogError("Client not configured for device flow", GrantType.DeviceFlow, request);
            return Invalid(request, OidcConstants.AuthorizeErrors.UnauthorizedClient);
        }

        return Valid(request);
    }

    private async Task<DeviceAuthorizationRequestValidationResult> ValidateScopeAsync(ValidatedDeviceAuthorizationRequest request, CT ct)
    {
        //////////////////////////////////////////////////////////
        // scope must be present
        //////////////////////////////////////////////////////////
        var scope = request.Raw.Get(OidcConstants.AuthorizeRequest.Scope);
        if (scope.IsMissing())
        {
            _logger.LogTrace("Client provided no scopes - checking allowed scopes list");

            if (!IEnumerableExtensions.IsNullOrEmpty(request.Client.AllowedScopes))
            {
                var clientAllowedScopes = new List<string>(request.Client.AllowedScopes);
                if (request.Client.AllowOfflineAccess)
                {
                    clientAllowedScopes.Add(IdentityServerConstants.StandardScopes.OfflineAccess);
                }
                scope = clientAllowedScopes.ToSpaceSeparatedString();
                _logger.LogTrace("Defaulting to: {scopes}", scope);
            }
            else
            {
                LogError("No allowed scopes configured for client", request);
                return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope);
            }
        }

        if (scope.Length > _options.InputLengthRestrictions.Scope)
        {
            LogError("scopes too long.", request);
            return Invalid(request, description: "Invalid scope");
        }

        request.RequestedScopes = scope.FromSpaceSeparatedString().Distinct().ToList();

        if (request.RequestedScopes.Contains(IdentityServerConstants.StandardScopes.OpenId))
        {
            request.IsOpenIdRequest = true;
        }

        //////////////////////////////////////////////////////////
        // check if scopes are valid/supported
        //////////////////////////////////////////////////////////
        var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = request.Client,
            Scopes = request.RequestedScopes
        }, ct);

        if (!validatedResources.Succeeded)
        {
            if (validatedResources.InvalidScopes.Count > 0)
            {
                return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope);
            }

            return Invalid(request, OidcConstants.AuthorizeErrors.UnauthorizedClient, "Invalid scope");
        }

        if (validatedResources.Resources.IdentityResources.Count > 0 && !request.IsOpenIdRequest)
        {
            LogError("Identity related scope requests, but no openid scope", request);
            return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope);
        }

        request.ValidatedResources = validatedResources;

        return Valid(request);
    }
}
