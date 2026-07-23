// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.IdentityResources;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Provides administrative operations for managing identity resources.
/// </summary>
public interface IIdentityResourceAdmin
{
    /// <summary>
    /// Creates a new identity resource.
    /// </summary>
    /// <param name="resource">The identity resource definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage ID and version on success, or validation/conflict errors.</returns>
    Task<SaveResult<Guid>> CreateAsync(IdentityResourceConfiguration resource, Ct ct);

    /// <summary>
    /// Gets an identity resource by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<IdentityResourceConfiguration>> GetAsync(Guid id, Ct ct);

    /// <summary>
    /// Gets an identity resource by its unique name.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<IdentityResourceConfiguration>> GetByNameAsync(string name, Ct ct);

    /// <summary>
    /// Updates an existing identity resource.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="resource">The updated identity resource definition.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> UpdateAsync(Guid id, IdentityResourceConfiguration resource, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes an identity resource.
    /// </summary>
    /// <param name="id">The storage identifier of the identity resource to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct);

    /// <summary>
    /// Queries identity resources with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<IdentityResourceListItem>> QueryAsync(QueryRequest<IdentityResourceFilter, IdentityResourceSortField> request, Ct ct);
}
