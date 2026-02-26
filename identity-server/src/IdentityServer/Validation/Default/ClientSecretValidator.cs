// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates a client secret using the registered secret validators and parsers
/// </summary>
public class ClientSecretValidator : IClientSecretValidator
{
    private readonly ILogger _logger;
    private readonly IClientStore _clients;
    private readonly IEventService _events;
    private readonly ISecretsListValidator _validator;
    private readonly ISecretsListParser _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientSecretValidator"/> class.
    /// </summary>
    /// <param name="clients">The clients.</param>
    /// <param name="parser">The parser.</param>
    /// <param name="validator">The validator.</param>
    /// <param name="events">The events.</param>
    /// <param name="logger">The logger.</param>
    public ClientSecretValidator(IClientStore clients, ISecretsListParser parser, ISecretsListValidator validator, IEventService events, ILogger<ClientSecretValidator> logger)
    {
        _clients = clients;
        _parser = parser;
        _validator = validator;
        _events = events;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ClientSecretValidationResult> ValidateAsync(HttpContext context, Ct ct)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("ClientSecretValidator.Validate");

        _logger.LogDebug("Start client validation");

        var fail = new ClientSecretValidationResult
        {
            IsError = true,
            Error = IdentityModel.OidcConstants.TokenErrors.InvalidClient
        };

        var parsedSecret = await _parser.ParseAsync(context, ct);
        if (parsedSecret == null)
        {
            await RaiseFailureEventAsync("unknown", "No client id found", ct);

            _logger.LogError("No client identifier found");

            fail.Error = IdentityModel.OidcConstants.TokenErrors.InvalidRequest;
            return fail;
        }

        // load client
        var client = await _clients.FindEnabledClientByIdAsync(parsedSecret.Id, ct);
        if (client == null)
        {
            await RaiseFailureEventAsync(parsedSecret.Id, "Unknown client", ct);

            _logger.LogError("No client with id '{clientId}' found. aborting", parsedSecret.Id);
            return fail;
        }

        SecretValidationResult secretValidationResult = null;
        if (!client.RequireClientSecret || client.IsImplicitOnly())
        {
            _logger.LogDebug("Public Client - skipping secret validation success");
        }
        else
        {
            secretValidationResult = await _validator.ValidateAsync(client.ClientSecrets, parsedSecret, ct);
            if (secretValidationResult.Success == false)
            {
                await RaiseFailureEventAsync(client.ClientId, "Invalid client secret", ct);
                _logger.LogError("Client secret validation failed for client: {clientId}.", client.ClientId);

                return fail;
            }
        }

        _logger.LogDebug("Client validation success");

        var success = new ClientSecretValidationResult
        {
            IsError = false,
            Client = client,
            Secret = parsedSecret,
            Confirmation = secretValidationResult?.Confirmation
        };

        await RaiseSuccessEventAsync(client.ClientId, parsedSecret.Type, ct);
        return success;
    }

    private Task RaiseSuccessEventAsync(string clientId, string authMethod, Ct ct)
    {
        Telemetry.Metrics.ClientSecretValidation(clientId, authMethod);
        return _events.RaiseAsync(new ClientAuthenticationSuccessEvent(clientId, authMethod), ct);
    }

    private Task RaiseFailureEventAsync(string clientId, string message, Ct ct)
    {
        Telemetry.Metrics.ClientSecretValidationFailure(clientId, message);
        return _events.RaiseAsync(new ClientAuthenticationFailureEvent(clientId, message), ct);
    }
}
