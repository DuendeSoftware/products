// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
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
    /// The CancellationToken provider.
    /// </summary>
    protected readonly ICancellationTokenProvider CancellationTokenProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<ResourceStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationTokenProvider"></param>
    /// <exception cref="ArgumentNullException">context</exception>
    public ResourceStore(IConfigurationDbContext context, ILogger<ResourceStore> logger, ICancellationTokenProvider cancellationTokenProvider)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        CancellationTokenProvider = cancellationTokenProvider;
    }

    /// <summary>
    /// Finds the API resources by name.
    /// </summary>
    /// <param name="apiResourceNames">The names.</param>
    /// <returns></returns>
    public virtual async Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
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

        var result = (await apis.ToArrayAsync(CancellationTokenProvider.CancellationToken))
            .Where(x => apiResourceNames.Contains(x.Name))
            .Select(x => x.ToModel()).ToArray();

        if (result.Length > 0)
        {
            Logger.LogDebug("Found {apis} API resource in database", result.Select(x => x.Name));
        }
        else
        {
            Logger.LogDebug("Did not find {apis} API resource in database", apiResourceNames);
        }

        return result;
    }

    /// <summary>
    /// Gets API resources by scope name.
    /// </summary>
    /// <param name="scopeNames"></param>
    /// <returns></returns>
    public virtual async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
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

        var results = (await apis.ToArrayAsync(CancellationTokenProvider.CancellationToken))
            .Where(api => api.Scopes.Any(x => names.Contains(x.Scope)));
        var models = results.Select(x => x.ToModel()).ToArray();

        Logger.LogDebug("Found {apis} API resources in database", models.Select(x => x.Name));

        return models;
    }

    /// <summary>
    /// Gets identity resources by scope name.
    /// </summary>
    /// <param name="scopeNames"></param>
    /// <returns></returns>
    public virtual async Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
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

        var results = (await resources.ToArrayAsync(CancellationTokenProvider.CancellationToken))
            .Where(x => scopes.Contains(x.Name));

        Logger.LogDebug("Found {scopes} identity scopes in database", results.Select(x => x.Name));

        return results.Select(x => x.ToModel()).ToArray();
    }

    /// <summary>
    /// Gets scopes by scope name.
    /// </summary>
    /// <param name="scopeNames"></param>
    /// <returns></returns>
    public virtual async Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
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

        var results = (await resources.ToArrayAsync(CancellationTokenProvider.CancellationToken))
            .Where(x => scopes.Contains(x.Name));

        Logger.LogDebug("Found {scopes} scopes in database", results.Select(x => x.Name));

        return results.Select(x => x.ToModel()).ToArray();
    }

    /// <summary>
    /// Gets all resources.
    /// </summary>
    /// <returns></returns>
    public virtual async Task<Resources> GetAllResourcesAsync()
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
            (await identity.ToArrayAsync(CancellationTokenProvider.CancellationToken)).Select(x => x.ToModel()),
            (await apis.ToArrayAsync(CancellationTokenProvider.CancellationToken)).Select(x => x.ToModel()),
            (await scopes.ToArrayAsync(CancellationTokenProvider.CancellationToken)).Select(x => x.ToModel())
        );

        Logger.LogDebug("Found {scopes} as all scopes, and {apis} as API resources",
            result.IdentityResources.Select(x => x.Name).Union(result.ApiScopes.Select(x => x.Name)),
            result.ApiResources.Select(x => x.Name));

        return result;
    }
}
