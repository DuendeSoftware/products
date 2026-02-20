// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// The introspection response generator
/// </summary>
/// <seealso cref="IIntrospectionResponseGenerator" />
public class IntrospectionResponseGenerator : IIntrospectionResponseGenerator
{
    /// <summary>
    /// Gets the events.
    /// </summary>
    /// <value>
    /// The events.
    /// </value>
    protected readonly IEventService Events;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntrospectionResponseGenerator" /> class.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <param name="logger">The logger.</param>
    public IntrospectionResponseGenerator(IEventService events, ILogger<IntrospectionResponseGenerator> logger)
    {
        Events = events;
        Logger = logger;
    }

    /// <summary>
    /// Processes the response.
    /// </summary>
    /// <param name="validationResult">The validation result.</param>
    /// <returns></returns>
    public virtual async Task<Dictionary<string, object>> ProcessAsync(IntrospectionRequestValidationResult validationResult)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("IntrospectionResponseGenerator.Process");

        Logger.LogTrace("Creating introspection response");

        // standard response
        var response = new Dictionary<string, object>
        {
            { "active", false }
        };

        var callerName = validationResult.Api?.Name ?? validationResult.Client.ClientId;

        // token is invalid
        if (validationResult.IsActive == false)
        {
            Logger.LogDebug("Creating introspection response for inactive token.");
            Telemetry.Metrics.Introspection(callerName, false);
            await Events.RaiseAsync(new TokenIntrospectionSuccessEvent(validationResult), default);

            return response;
        }

        // client can see all their own scopes
        var scopes = validationResult.Claims.Where(c => c.Type == JwtClaimTypes.Scope).Select(x => x.Value);

        if (validationResult.Api != null)
        {
            // expected scope not present
            if (await AreExpectedScopesPresentAsync(validationResult) == false)
            {
                return response;
            }

            // calculate scopes the API is allowed to see
            var allowedScopes = validationResult.Api.Scopes;
            scopes = scopes.Where(x => allowedScopes.Contains(x));
        }

        Logger.LogDebug("Creating introspection response for active token.");

        // get all claims (without scopes)
        response = validationResult.Claims.Where(c => c.Type != JwtClaimTypes.Scope).ToClaimsDictionary();

        // add active flag
        response.Add("active", true);

        // add scopes
        response.Add("scope", scopes.ToSpaceSeparatedString());

        Telemetry.Metrics.Introspection(callerName, true);
        await Events.RaiseAsync(new TokenIntrospectionSuccessEvent(validationResult), default);
        return response;
    }

    /// <summary>
    /// Checks if the API resource is allowed to introspect the scopes.
    /// </summary>
    /// <param name="validationResult">The validation result.</param>
    /// <returns></returns>
    protected virtual async Task<bool> AreExpectedScopesPresentAsync(IntrospectionRequestValidationResult validationResult)
    {
        var apiScopes = validationResult.Api.Scopes;
        var tokenScopes = validationResult.Claims.Where(c => c.Type == JwtClaimTypes.Scope);

        var tokenScopesThatMatchApi = tokenScopes.Where(c => apiScopes.Contains(c.Value));

        var result = false;

        if (tokenScopesThatMatchApi.Any())
        {
            // at least one of the scopes the API supports is in the token
            result = true;
        }
        else
        {
            // no scopes for this API are found in the token
            Logger.LogError("Expected scope {scopes} is missing in token", apiScopes);

            const string errorMessage = "Expected scopes are missing";
            var callerName = validationResult.Api?.Name ?? validationResult.Client.ClientId;
            Telemetry.Metrics.IntrospectionFailure(callerName, errorMessage);
            await Events.RaiseAsync(new TokenIntrospectionFailureEvent(validationResult.Api.Name, errorMessage, validationResult.Token, apiScopes, tokenScopes.Select(s => s.Value)), default);
        }

        return result;
    }
}
