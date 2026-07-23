// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiResources;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Provides administrative operations for managing API resources.
/// </summary>
public interface IApiResourceAdmin
{
    /// <summary>
    /// Creates a new API resource.
    /// </summary>
    /// <param name="resource">The API resource definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage ID and version on success, or validation/conflict errors.</returns>
    Task<SaveResult<Guid>> CreateAsync(ApiResourceConfiguration resource, Ct ct);

    /// <summary>
    /// Gets an API resource by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ApiResourceConfiguration>> GetAsync(Guid id, Ct ct);

    /// <summary>
    /// Gets an API resource by its unique name.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ApiResourceConfiguration>> GetByNameAsync(string name, Ct ct);

    /// <summary>
    /// Updates an existing API resource.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="resource">The updated API resource definition.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> UpdateAsync(Guid id, ApiResourceConfiguration resource, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes an API resource.
    /// </summary>
    /// <param name="id">The storage identifier of the API resource to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct);

    /// <summary>
    /// Queries API resources with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<ApiResourceListItem>> QueryAsync(QueryRequest<ApiResourceFilter, ApiResourceSortField> request, Ct ct);

    /// <summary>
    /// Creates a new secret for an API resource. The plaintext value is hashed before storage.
    /// </summary>
    /// <param name="apiResourceId">The storage ID of the API resource.</param>
    /// <param name="plaintextValue">The plaintext secret (will be hashed before storage).</param>
    /// <param name="hashAlgorithm">Hash algorithm to use (defaults to <see cref="SecretHashAlgorithm.Sha256"/>).</param>
    /// <param name="description">Optional description.</param>
    /// <param name="expiration">Optional expiration date.</param>
    /// <param name="type">Secret type (defaults to <c>"SharedSecret"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new secret's storage <see cref="Guid"/> on success, or errors on failure.</returns>
    Task<SaveResult<Guid>> CreateSecretAsync(
        Guid apiResourceId,
        string plaintextValue,
        SecretHashAlgorithm? hashAlgorithm,
        string? description,
        DateTime? expiration,
        string? type,
        Ct ct);

    /// <summary>
    /// Deletes a secret from an API resource.
    /// </summary>
    /// <param name="apiResourceId">The storage ID of the API resource.</param>
    /// <param name="secretId">The storage ID of the secret to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<Guid>> DeleteSecretAsync(Guid apiResourceId, Guid secretId, Ct ct);
}
