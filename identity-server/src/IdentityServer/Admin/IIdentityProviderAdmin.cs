// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Provides administrative operations for managing external identity providers.
/// </summary>
public interface IIdentityProviderAdmin
{
    /// <summary>
    /// Creates a new identity provider.
    /// </summary>
    /// <param name="provider">The identity provider definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage ID and version on success, or validation/conflict errors.</returns>
    Task<SaveResult<Guid>> CreateAsync(IdentityProviderConfiguration provider, Ct ct);

    /// <summary>
    /// Gets an identity provider by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<IdentityProviderConfiguration>> GetAsync(Guid id, Ct ct);

    /// <summary>
    /// Gets an identity provider by its authentication scheme name.
    /// </summary>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<IdentityProviderConfiguration>> GetBySchemeAsync(string scheme, Ct ct);

    /// <summary>
    /// Updates an existing identity provider. The model is mutable — callers can Get, modify, and Update.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="provider">The updated identity provider definition.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> UpdateAsync(Guid id, IdentityProviderConfiguration provider, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes an identity provider.
    /// </summary>
    /// <param name="id">The storage identifier of the identity provider to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct);

    /// <summary>
    /// Queries identity providers with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<IdentityProviderListItem>> QueryAsync(QueryRequest<IdentityProviderFilter, IdentityProviderSortField> request, Ct ct);
}
