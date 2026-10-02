// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of IResourceStore thats uses EF.
/// </summary>
/// <seealso cref="IResourceStore" />
public class ResourceStore : IResourceStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IConfigurationDbContext Context;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<ResourceStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException">context</exception>
    public ResourceStore(IConfigurationDbContext context, ILogger<ResourceStore> logger)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
    }

    /// <summary>
    /// Finds the API resources by name.
    /// </summary>
    /// <param name="apiResourceNames">The names.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindApiResourcesByName");
        activity?.SetTag(Tracing.Properties.ApiResourceNames, apiResourceNames.ToSpaceSeparatedString());

        ArgumentNullException.ThrowIfNull(apiResourceNames);

        var query =
            from apiResource in Context.ApiResources
            where apiResourceNames.Contains(apiResource.Name)
            select apiResource;

        var apis = query
            .Include(x => x.Secrets)
            .Include(x => x.Scopes)
            .Include(x => x.UserClaims)
            .Include(x => x.Properties)
            .AsNoTracking();

        var result = (await apis.ToArrayAsync(ct))
            .Where(x => apiResourceNames.Contains(x.Name))
            .Select(x => x.ToModel()).ToArray();

        if (result.Length > 0)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                var apiNames = result.Select(x => x.Name);
                Log.FoundValueApiResourceInDatabase(Logger, apiNames);
            }
        }
        else
        {
            Log.DidNotFindValueApiResourceInDatabase(Logger, apiResourceNames);
        }

        return result;
    }

    /// <summary>
    /// Gets API resources by scope name.
    /// </summary>
    /// <param name="scopeNames"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindApiResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var names = scopeNames.ToArray();

        var query =
            from api in Context.ApiResources
            where api.Scopes.Any(x => names.Contains(x.Scope))
            select api;

        var apis = query
            .Include(x => x.Secrets)
            .Include(x => x.Scopes)
            .Include(x => x.UserClaims)
            .Include(x => x.Properties)
            .AsNoTracking();

        var results = (await apis.ToArrayAsync(ct))
            .Where(api => api.Scopes.Any(x => names.Contains(x.Scope)));
        var models = results.Select(x => x.ToModel()).ToArray();

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            var apiNames = models.Select(x => x.Name);
            Log.FoundValueApiResourcesInDatabase(Logger, apiNames);
        }

        return models;
    }

    /// <summary>
    /// Gets identity resources by scope name.
    /// </summary>
    /// <param name="scopeNames"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindIdentityResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var scopes = scopeNames.ToArray();

        var query =
            from identityResource in Context.IdentityResources
            where scopes.Contains(identityResource.Name)
            select identityResource;

        var resources = query
            .Include(x => x.UserClaims)
            .Include(x => x.Properties)
            .AsNoTracking();

        var results = (await resources.ToArrayAsync(ct))
            .Where(x => scopes.Contains(x.Name));

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            var identityScopeNames = results.Select(x => x.Name);
            Log.FoundValueIdentityScopesInDatabase(Logger, identityScopeNames);
        }

        return results.Select(x => x.ToModel()).ToArray();
    }

    /// <summary>
    /// Gets scopes by scope name.
    /// </summary>
    /// <param name="scopeNames"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindApiScopesByName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var scopes = scopeNames.ToArray();

        var query =
            from scope in Context.ApiScopes
            where scopes.Contains(scope.Name)
            select scope;

        var resources = query
            .Include(x => x.UserClaims)
            .Include(x => x.Properties)
            .AsNoTracking();

        var results = (await resources.ToArrayAsync(ct))
            .Where(x => scopes.Contains(x.Name));

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            var apiScopeNames = results.Select(x => x.Name);
            Log.FoundValueScopesInDatabase(Logger, apiScopeNames);
        }

        return results.Select(x => x.ToModel()).ToArray();
    }

    /// <summary>
    /// Gets all resources.
    /// </summary>
    /// <returns></returns>
    public virtual async Task<Resources> GetAllResourcesAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.GetAllResources");

        var identity = Context.IdentityResources
            .Include(x => x.UserClaims)
            .Include(x => x.Properties)
            .AsNoTracking();

        var apis = Context.ApiResources
            .Include(x => x.Secrets)
            .Include(x => x.Scopes)
            .Include(x => x.UserClaims)
            .Include(x => x.Properties)
            .AsNoTracking();

        var scopes = Context.ApiScopes
            .Include(x => x.UserClaims)
            .Include(x => x.Properties)
            .AsNoTracking();

        var result = new Resources(
            (await identity.ToArrayAsync(ct)).Select(x => x.ToModel()),
            (await apis.ToArrayAsync(ct)).Select(x => x.ToModel()),
            (await scopes.ToArrayAsync(ct)).Select(x => x.ToModel())
        );

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            var allScopeNames = result.IdentityResources.Select(x => x.Name)
                .Union(result.ApiScopes.Select(x => x.Name));
            var apiResourceNames = result.ApiResources.Select(x => x.Name);
            Log.FoundValueAsAllScopesAndValueAs(
                Logger,
                allScopeNames,
                apiResourceNames);
        }

        return result;
    }
}
