// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.ApiResources;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.IdentityServer.Stores.Storage.IdentityResources;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.Resources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class StorageResourceStore(
    ApiResourceRepository apiResourceRepository,
    ApiScopeRepository apiScopeRepository,
    IdentityResourceRepository identityResourceRepository,
    ILogger<StorageResourceStore> logger) : IResourceStore
{
    private const int PageSize = 200;

    public async Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindIdentityResourcesByScopeName");

        var names = scopeNames.ToList();
        activity?.SetTag(Tracing.Properties.ScopeNames, names.ToSpaceSeparatedString());

        if (names.Count == 0)
        {
            return [];
        }

        var dsos = await identityResourceRepository.FindByNamesAsync(names, ct);
        var results = dsos.Select(MapToIdentityResource).ToList();

        logger.IdentityResourcesFound(LogLevel.Debug, results.Select(x => x.Name));

        return results;
    }

    public async Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindApiScopesByName");

        var names = scopeNames.ToList();
        activity?.SetTag(Tracing.Properties.ScopeNames, names.ToSpaceSeparatedString());

        if (names.Count == 0)
        {
            return [];
        }

        var dsos = await apiScopeRepository.FindByNamesAsync(names, ct);
        var results = dsos.Select(MapToApiScope).ToList();

        logger.ApiScopesFound(LogLevel.Debug, results.Select(x => x.Name));

        return results;
    }

    public async Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindApiResourcesByScopeName");

        var names = scopeNames.ToList();
        activity?.SetTag(Tracing.Properties.ScopeNames, names.ToSpaceSeparatedString());

        if (names.Count == 0)
        {
            return [];
        }

        var dsos = await apiResourceRepository.FindByScopeNamesAsync(names, ct);
        var results = dsos.Select(MapToApiResource).ToList();

        logger.ApiResourcesFound(LogLevel.Debug, results.Select(x => x.Name));

        return results;
    }

    public async Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.FindApiResourcesByName");

        var names = apiResourceNames.ToList();
        activity?.SetTag(Tracing.Properties.ApiResourceNames, names.ToSpaceSeparatedString());

        if (names.Count == 0)
        {
            return [];
        }

        var dsos = await apiResourceRepository.FindByNamesAsync(names, ct);
        var results = dsos.Select(MapToApiResource).ToList();

        if (results.Count > 0)
        {
            logger.ApiResourcesFound(LogLevel.Debug, results.Select(x => x.Name));
        }
        else
        {
            logger.ApiResourcesNotFound(LogLevel.Debug, names);
        }

        return results;
    }

    public async Task<Models.Resources> GetAllResourcesAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ResourceStore.GetAllResources");

        var identityResources = await GetAllIdentityResourcesAsync(ct);
        var apiResources = await GetAllApiResourcesAsync(ct);
        var apiScopes = await GetAllApiScopesAsync(ct);

        var result = new Models.Resources(identityResources, apiResources, apiScopes);

        logger.AllResourcesFound(LogLevel.Debug,
            result.IdentityResources.Select(x => x.Name).Union(result.ApiScopes.Select(x => x.Name)),
            result.ApiResources.Select(x => x.Name));

        return result;
    }

    private async Task<List<IdentityResource>> GetAllIdentityResourcesAsync(Ct ct)
    {
        var results = new List<IdentityResource>();
        var pageNumber = 1;

        while (true)
        {
            var range = DataRange.FromPage(pageNumber, PageSize);
            var request = QueryRequest.Create<IdentityResourceFilter, IdentityResourceSortField>(range);
            var result = await identityResourceRepository.QueryAsync(request, ct);

            foreach (var dso in result.Items)
            {
                results.Add(MapToIdentityResource(dso));
            }

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        return results;
    }

    private async Task<List<ApiResource>> GetAllApiResourcesAsync(Ct ct)
    {
        var results = new List<ApiResource>();
        var pageNumber = 1;

        while (true)
        {
            var range = DataRange.FromPage(pageNumber, PageSize);
            var request = QueryRequest.Create<ApiResourceFilter, ApiResourceSortField>(range);
            var result = await apiResourceRepository.QueryAsync(request, ct);

            foreach (var dso in result.Items)
            {
                results.Add(MapToApiResource(dso));
            }

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        return results;
    }

    private async Task<List<ApiScope>> GetAllApiScopesAsync(Ct ct)
    {
        var results = new List<ApiScope>();
        var pageNumber = 1;

        while (true)
        {
            var range = DataRange.FromPage(pageNumber, PageSize);
            var request = QueryRequest.Create<ApiScopeFilter, ApiScopeSortField>(range);
            var result = await apiScopeRepository.QueryAsync(request, ct);

            foreach (var dso in result.Items)
            {
                results.Add(MapToApiScope(dso));
            }

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        return results;
    }

    private static ApiResource MapToApiResource(ApiResourceDso.V1 dso) => new()
    {
        Name = dso.Name,
        Enabled = dso.Enabled,
        DisplayName = dso.DisplayName,
        Description = dso.Description,
        ShowInDiscoveryDocument = dso.ShowInDiscoveryDocument,
        RequireResourceIndicator = dso.RequireResourceIndicator,
        UserClaims = new HashSet<string>(dso.UserClaims),
        Scopes = new HashSet<string>(dso.Scopes.Select(s => s.Name)),
        AllowedAccessTokenSigningAlgorithms = new HashSet<string>(dso.AllowedAccessTokenSigningAlgorithms),
        Properties = EavPropertyMapper.ExtractStringProperties(dso.ExtendedAttributeValues),
        ApiSecrets = dso.ApiSecrets.Select(s => new Secret
        {
            Value = s.Value,
            Description = s.Description,
            Expiration = s.Expiration,
            Type = s.Type
        }).ToList()
    };

    private static ApiScope MapToApiScope(ApiScopeDso.V1 dso) => new()
    {
        Name = dso.Name,
        Enabled = dso.Enabled,
        DisplayName = dso.DisplayName,
        Description = dso.Description,
        ShowInDiscoveryDocument = dso.ShowInDiscoveryDocument,
        Required = dso.Required,
        Emphasize = dso.Emphasize,
        UserClaims = new HashSet<string>(dso.UserClaims),
        Properties = EavPropertyMapper.ExtractStringProperties(dso.ExtendedAttributeValues),
    };

    private static IdentityResource MapToIdentityResource(IdentityResourceDso.V1 dso) => new()
    {
        Name = dso.Name,
        Enabled = dso.Enabled,
        DisplayName = dso.DisplayName,
        Description = dso.Description,
        ShowInDiscoveryDocument = dso.ShowInDiscoveryDocument,
        Required = dso.Required,
        Emphasize = dso.Emphasize,
        UserClaims = new HashSet<string>(dso.UserClaims),
        Properties = EavPropertyMapper.ExtractStringProperties(dso.ExtendedAttributeValues),
    };
}
