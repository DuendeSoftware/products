// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default implementation of IResourceValidator.
/// </summary>
public class DefaultResourceValidator : IResourceValidator
{
    private readonly ILogger _logger;
    private readonly IScopeParser _scopeParser;
    private readonly IResourceStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultResourceValidator"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="scopeParser"></param>
    /// <param name="logger">The logger.</param>
    public DefaultResourceValidator(IResourceStore store, IScopeParser scopeParser, ILogger<DefaultResourceValidator> logger)
    {
        _logger = logger;
        _scopeParser = scopeParser;
        _store = store;
    }

    /// <inheritdoc/>
    public virtual async Task<ResourceValidationResult> ValidateRequestedResourcesAsync(ResourceValidationRequest request, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = Tracing.ValidationActivitySource.StartActivity("DefaultResourceValidator.ValidateRequestedResources");
        activity?.SetTag(Tracing.Properties.Scope, request.Scopes.ToSpaceSeparatedString());
        activity?.SetTag(Tracing.Properties.Resource, request.ResourceIndicators.ToSpaceSeparatedString());

        var result = new ResourceValidationResult();

        var parsedScopesResult = _scopeParser.ParseScopeValues(request.Scopes);
        if (!parsedScopesResult.Succeeded)
        {
            foreach (var invalidScope in parsedScopesResult.Errors)
            {
                _logger.LogError("Invalid parsed scope {scope}, message: {error}", invalidScope.RawValue, invalidScope.Error);
                result.InvalidScopes.Add(invalidScope.RawValue);
            }

            return result;
        }

        var scopeNames = parsedScopesResult.ParsedScopes.Select(x => x.ParsedName).Distinct().ToArray();
        // todo: this API might want to pass resource indicators to better filter
        var scopeResourcesFromStore = await _store.FindEnabledResourcesByScopeAsync(scopeNames, ct);

        if (request.ResourceIndicators?.Any() == true)
        {
            // remove isolated API resources not included in the requested resource indicators
            // and keep any ApiResources that aren't marked as isolated
            //
            // todo: maybe add a "strict" resource semantics option to filter out shared api resources?
            //
            scopeResourcesFromStore.ApiResources = scopeResourcesFromStore.ApiResources
                .Where(x => !x.RequireResourceIndicator || request.ResourceIndicators.Contains(x.Name))
                .ToHashSet();

            // find requested resource indicators not matched by scope
            var matchedApiResourceNames = scopeResourcesFromStore.ApiResources.Select(x => x.Name).ToArray();
            var invalidRequestedResourceIndicators = request.ResourceIndicators.Except(matchedApiResourceNames);
            if (invalidRequestedResourceIndicators.Any())
            {
                foreach (var invalid in invalidRequestedResourceIndicators)
                {
                    _logger.LogError("Invalid resource identifier {resource}. It is either not found, not enabled, or does not support any of the requested scopes.", invalid);
                    result.InvalidResourceIndicators.Add(invalid);
                }

                return result;
            }
        }
        else
        {
            // no resource indicators, so filter all API resources marked as isolated
            scopeResourcesFromStore.ApiResources = scopeResourcesFromStore.ApiResources.Where(x => !x.RequireResourceIndicator).ToHashSet();
        }


        foreach (var scope in parsedScopesResult.ParsedScopes)
        {
            await ValidateScopeAsync(request.Client, scopeResourcesFromStore, scope, result);
        }

        // TODO: consider validating resources requested against client w/ some ValidateResource API

        if (result.InvalidScopes.Count > 0 || result.InvalidResourceIndicators.Count > 0)
        {
            result.Resources.IdentityResources.Clear();
            result.Resources.ApiResources.Clear();
            result.Resources.ApiScopes.Clear();
            result.ParsedScopes.Clear();
        }


        return result;
    }

    /// <summary>
    /// Validates that the requested scopes is contained in the store, and the client is allowed to request it.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="resourcesFromStore"></param>
    /// <param name="requestedScope"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    protected virtual async Task ValidateScopeAsync(
        Client client,
        Resources resourcesFromStore,
        ParsedScopeValue requestedScope,
        ResourceValidationResult result)
    {
        if (requestedScope.ParsedName == IdentityServerConstants.StandardScopes.OfflineAccess)
        {
            if (await IsClientAllowedOfflineAccessAsync(client))
            {
                result.Resources.OfflineAccess = true;
                result.ParsedScopes.Add(new ParsedScopeValue(IdentityServerConstants.StandardScopes.OfflineAccess));
            }
            else
            {
                result.InvalidScopes.Add(IdentityServerConstants.StandardScopes.OfflineAccess);
            }
        }
        else
        {
            var identity = resourcesFromStore.FindIdentityResourcesByScope(requestedScope.ParsedName);
            if (identity != null)
            {
                if (await IsClientAllowedIdentityResourceAsync(client, identity))
                {
                    result.ParsedScopes.Add(requestedScope);
                    result.Resources.IdentityResources.Add(identity);
                }
                else
                {
                    result.InvalidScopes.Add(requestedScope.RawValue);
                }
            }
            else
            {
                var apiScope = resourcesFromStore.FindApiScope(requestedScope.ParsedName);
                if (apiScope != null)
                {
                    if (await IsClientAllowedApiScopeAsync(client, apiScope))
                    {
                        result.ParsedScopes.Add(requestedScope);
                        result.Resources.ApiScopes.Add(apiScope);

                        var apis = resourcesFromStore.FindApiResourcesByScope(apiScope.Name);
                        foreach (var api in apis)
                        {
                            result.Resources.ApiResources.Add(api);
                        }
                    }
                    else
                    {
                        result.InvalidScopes.Add(requestedScope.RawValue);
                    }
                }
                else
                {
                    _logger.LogError("Scope {scope} not found in store or not supported by requested resource indicators.", requestedScope.ParsedName);
                    result.InvalidScopes.Add(requestedScope.RawValue);
                }
            }
        }
    }

    /// <summary>
    /// Determines if client is allowed access to the identity scope.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="identity"></param>
    /// <returns></returns>
    protected virtual Task<bool> IsClientAllowedIdentityResourceAsync(Client client, IdentityResource identity)
    {
        var allowed = client.AllowedScopes.Contains(identity.Name);
        if (!allowed)
        {
            _logger.LogError("Client {client} is not allowed access to scope {scope}.", client.ClientId, identity.Name);
        }
        return Task.FromResult(allowed);
    }

    /// <summary>
    /// Determines if client is allowed access to the API scope.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="apiScope"></param>
    /// <returns></returns>
    protected virtual Task<bool> IsClientAllowedApiScopeAsync(Client client, ApiScope apiScope)
    {
        var allowed = client.AllowedScopes.Contains(apiScope.Name);
        if (!allowed)
        {
            _logger.LogError("Client {client} is not allowed access to scope {scope}.", client.ClientId, apiScope.Name);
        }
        return Task.FromResult(allowed);
    }

    /// <summary>
    /// Validates if the client is allowed offline_access.
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    protected virtual Task<bool> IsClientAllowedOfflineAccessAsync(Client client)
    {
        var allowed = client.AllowOfflineAccess;
        if (!allowed)
        {
            _logger.LogError("Client {client} is not allowed access to scope offline_access (via AllowOfflineAccess setting).", client.ClientId);
        }
        return Task.FromResult(allowed);
    }
}
