// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiScopes;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Provides administrative operations for managing API scopes.
/// </summary>
public interface IApiScopeAdmin
{
    /// <summary>
    /// Creates a new API scope.
    /// </summary>
    /// <param name="scope">The API scope definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage ID and version on success, or validation/conflict errors.</returns>
    Task<SaveResult<Guid>> CreateAsync(ApiScopeConfiguration scope, Ct ct);

    /// <summary>
    /// Gets an API scope by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ApiScopeConfiguration>> GetAsync(Guid id, Ct ct);

    /// <summary>
    /// Gets an API scope by its unique name.
    /// </summary>
    /// <param name="name">The scope name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ApiScopeConfiguration>> GetByNameAsync(string name, Ct ct);

    /// <summary>
    /// Updates an existing API scope.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="scope">The updated API scope definition.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> UpdateAsync(Guid id, ApiScopeConfiguration scope, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes an API scope.
    /// </summary>
    /// <param name="id">The storage identifier of the API scope to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct);

    /// <summary>
    /// Queries API scopes with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<ApiScopeListItem>> QueryAsync(QueryRequest<ApiScopeFilter, ApiScopeSortField> request, Ct ct);
}
