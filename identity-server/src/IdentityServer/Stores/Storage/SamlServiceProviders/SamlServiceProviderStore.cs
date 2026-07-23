// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.SamlServiceProviders;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SamlServiceProviderStore(
    SamlServiceProviderRepository repository,
    ILogger<SamlServiceProviderStore> logger) : ISamlServiceProviderStore
{
    private const int PageSize = 200;

    /// <inheritdoc/>
    public async Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlServiceProviderStore.FindByEntityId");
        activity?.SetTag(Tracing.Properties.SamlEntityId, entityId);

        var result = await repository.TryReadByEntityIdAsync(entityId, ct);
        if (result is null)
        {
            logger.SamlServiceProviderNotFound(LogLevel.Debug, entityId);
            return null;
        }

        logger.SamlServiceProviderFound(LogLevel.Debug, entityId);
        return MapToModel(result.Value.Dso);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlServiceProviderStore.GetAllSamlServiceProviders");

        var pageNumber = 1;
        var count = 0;

        while (true)
        {
            var range = DataRange.FromPage(pageNumber, PageSize);
            var request = QueryRequest.Create<SamlServiceProviderFilter, SamlServiceProviderSortField>(range);
            var result = await repository.QueryAsync(request, ct);

            foreach (var dso in result.Items)
            {
                count++;
                yield return MapToModel(dso);
            }

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        logger.SamlServiceProvidersRetrieved(LogLevel.Debug, count);
    }

    private static SamlServiceProvider MapToModel(SamlServiceProviderDso.V1 dso) => new()
    {
        EntityId = dso.EntityId,
        Enabled = dso.Enabled,
        DisplayName = dso.DisplayName,
        Description = dso.Description,

        // Timing
        ClockSkew = dso.ClockSkewTicks.HasValue ? TimeSpan.FromTicks(dso.ClockSkewTicks.Value) : null,
        RequestMaxAge = dso.RequestMaxAgeTicks.HasValue ? TimeSpan.FromTicks(dso.RequestMaxAgeTicks.Value) : null,
        AssertionLifetime = dso.AssertionLifetimeTicks.HasValue ? TimeSpan.FromTicks(dso.AssertionLifetimeTicks.Value) : null,

        // Endpoints
        AssertionConsumerServiceUrls = dso.AssertionConsumerServiceUrls
            .Select(a => new IndexedEndpoint
            {
                Location = a.Location,
                Binding = (SamlBinding)a.Binding,
                Index = a.Index,
                IsDefault = a.IsDefault
            })
            .ToHashSet(),
        SingleLogoutServiceUrls = dso.SingleLogoutServiceUrls
            .Select(s => new SamlEndpointType
            {
                Location = s.Location,
                Binding = (SamlBinding)s.Binding
            })
            .ToHashSet(),

        // Security
        RequireSignedAuthnRequests = dso.RequireSignedAuthnRequests,
        RequireSignedLogoutResponses = dso.RequireSignedLogoutResponses,
        Certificates = dso.Certificates
            .Select(c =>
            {
                var bytes = Convert.FromBase64String(c.Base64Data);
                var x509 = X509CertificateLoader.LoadCertificate(bytes);
                return new ServiceProviderCertificate
                {
                    Certificate = x509,
                    Use = (KeyUse)c.Use
                };
            })
            .ToList(),

        // SSO
        AllowIdpInitiated = dso.AllowIdpInitiated,

        // Scopes
        AllowedScopes = new HashSet<string>(dso.AllowedScopes),

        // Claims
        ClaimMappings = new Dictionary<string, string>(dso.ClaimMappings),
        AuthnContextMappings = new Dictionary<string, string>(dso.AuthnContextMappings),
        RequestedClaimTypes = [.. dso.RequestedClaimTypes],

        // NameID
        DefaultNameIdFormat = dso.DefaultNameIdFormat,
        EmailNameIdClaimType = dso.EmailNameIdClaimType,

        // Signing
        SigningBehavior = dso.SigningBehavior.HasValue ? (SamlSigningBehavior)dso.SigningBehavior.Value : null,
        AllowedSignatureAlgorithms = dso.AllowedSignatureAlgorithms.Count > 0
            ? [.. dso.AllowedSignatureAlgorithms]
            : null
    };
}
