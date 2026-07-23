// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Provides administrative operations for managing SAML Service Providers.
/// </summary>
public interface ISamlServiceProviderAdmin
{
    /// <summary>
    /// Creates a new SAML Service Provider.
    /// </summary>
    /// <param name="serviceProvider">The Service Provider definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage ID and version on success, or validation/conflict errors.</returns>
    Task<SaveResult<Guid>> CreateAsync(SamlServiceProviderConfiguration serviceProvider, Ct ct);

    /// <summary>
    /// Gets a SAML Service Provider by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<SamlServiceProviderConfiguration>> GetAsync(Guid id, Ct ct);

    /// <summary>
    /// Gets a SAML Service Provider by its SAML entity ID.
    /// </summary>
    /// <param name="entityId">The SAML entity ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<SamlServiceProviderConfiguration>> GetByEntityIdAsync(string entityId, Ct ct);

    /// <summary>
    /// Updates an existing SAML Service Provider. The model is mutable — callers can Get, modify, and Update.
    /// Certificates are managed inline — the full list is replaced on update.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="serviceProvider">The updated Service Provider definition.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> UpdateAsync(Guid id, SamlServiceProviderConfiguration serviceProvider, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes a SAML Service Provider.
    /// </summary>
    /// <param name="id">The storage identifier of the Service Provider to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct);

    /// <summary>
    /// Queries SAML Service Providers with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<SamlServiceProviderListItem>> QueryAsync(QueryRequest<SamlServiceProviderFilter, SamlServiceProviderSortField> request, Ct ct);
}
