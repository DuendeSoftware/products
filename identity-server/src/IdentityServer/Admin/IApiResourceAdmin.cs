// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiResources;
using Duende.Storage;
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
    Task<SaveResult<ApiResourceId>> CreateAsync(CreateApiResource resource, Ct ct);

    /// <summary>
    /// Gets an API resource by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ApiResourceConfiguration>> GetAsync(ApiResourceId id, Ct ct);

    /// <summary>
    /// Gets an API resource by its unique name.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ApiResourceConfiguration>> GetByNameAsync(string name, Ct ct);

    /// <summary>
    /// Updates an existing API resource. Secret metadata cannot be updated.
    /// To add or change secret values, use <see cref="CreateSecretAsync"/> and <see cref="DeleteSecretAsync"/>.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="resource">The updated API resource definition.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<ApiResourceId>> UpdateAsync(ApiResourceId id, UpdateApiResource resource, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes an API resource.
    /// </summary>
    /// <param name="id">The storage identifier of the API resource to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<ApiResourceId>> DeleteAsync(ApiResourceId id, Ct ct);

    /// <summary>
    /// Queries API resources with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<ApiResourceListItem>> QueryAsync(QueryRequest<ApiResourceFilter, ApiResourceSortField> request, Ct ct);

    /// <summary>
    /// Creates a new secret for an API resource.
    /// For the built-in key/certificate types (<c>X509Thumbprint</c>, <c>X509Name</c>, <c>X509CertificateBase64</c>, and
    /// <c>JWK</c>, i.e. <see cref="IdentityServerConstants.SecretTypes.X509CertificateThumbprint"/>,
    /// <see cref="IdentityServerConstants.SecretTypes.X509CertificateName"/>,
    /// <see cref="IdentityServerConstants.SecretTypes.X509CertificateBase64"/>, and
    /// <see cref="IdentityServerConstants.SecretTypes.JsonWebKey"/>), the value is stored verbatim (not hashed);
    /// callers are responsible for supplying appropriate material for these types. All other types, including
    /// <c>SharedSecret</c> and the default (<see langword="null"/>) type, are hashed before storage.
    /// </summary>
    /// <param name="apiResourceId">The storage ID of the API resource.</param>
    /// <param name="plaintextValue">The plaintext secret value.</param>
    /// <param name="hashAlgorithm">Hash algorithm to use when the value is hashed (defaults to <see cref="SecretHashAlgorithm.Sha256"/>).</param>
    /// <param name="description">Optional description.</param>
    /// <param name="expiration">Optional expiration date.</param>
    /// <param name="type">Secret type (defaults to <c>"SharedSecret"</c>). See summary for which types are stored verbatim vs. hashed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new secret's storage <see cref="ApiResourceSecretId"/> on success, or errors on failure.</returns>
    Task<SaveResult<ApiResourceSecretId>> CreateSecretAsync(
        ApiResourceId apiResourceId,
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
    Task<SaveResult<ApiResourceSecretId>> DeleteSecretAsync(ApiResourceId apiResourceId, ApiResourceSecretId secretId, Ct ct);
}
