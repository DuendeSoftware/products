// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Models;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class IdentityProviderStore(
    IdentityProviderRepository repository,
    IIdentityProviderFactory identityProviderFactory,
    ILogger<IdentityProviderStore> logger) : IIdentityProviderStore
{
    private const int PageSize = 200;

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("IdentityProviderStore.GetAllSchemeNames");

        var names = new List<IdentityProviderName>();
        var pageNumber = 1;

        while (true)
        {
            var range = DataRange.FromPage(pageNumber, PageSize);
            var request = QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(range);
            var result = await repository.QueryAsync(request, ct);

            foreach (var dso in result.Items)
            {
                names.Add(new IdentityProviderName
                {
                    Scheme = dso.Scheme,
                    DisplayName = dso.DisplayName,
                    Enabled = dso.Enabled
                });
            }

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        logger.SchemesRetrieved(LogLevel.Debug, names.Count);

        return names;
    }

    /// <inheritdoc/>
    public async Task<IdentityProvider?> GetBySchemeAsync(string scheme, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("IdentityProviderStore.GetByScheme");
        activity?.SetTag(Tracing.Properties.Scheme, scheme);

        var result = await repository.TryReadBySchemeAsync(scheme, ct);
        if (result is null)
        {
            return null;
        }

        var dso = result.Value.Dso;
        var baseProvider = MapToBaseProvider(dso);
        var provider = identityProviderFactory.Create(baseProvider);

        if (provider is null)
        {
            logger.IdentityProviderMappingFailed(LogLevel.Error, scheme, dso.Type);
            return baseProvider;
        }

        return provider;
    }

    private static IdentityProvider MapToBaseProvider(IdentityProviderDso.V1 dso) =>
        new(dso.Type)
        {
            Scheme = dso.Scheme,
            DisplayName = dso.DisplayName,
            Enabled = dso.Enabled,
            Properties = new Dictionary<string, string>(dso.Properties)
        };
}
