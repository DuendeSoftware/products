// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.Clients;
using Duende.Storage;
using Duende.Storage.Querying;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Provides administrative operations for managing OAuth/OIDC clients.
/// </summary>
public interface IClientAdmin
{
    // === Client CRUD ===

    /// <summary>
    /// Creates a new client.
    /// </summary>
    /// <param name="client">The client definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage ID and version on success, or validation/conflict errors.</returns>
    Task<SaveResult<ClientId>> CreateAsync(CreateClient client, Ct ct);

    /// <summary>
    /// Gets a client by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ClientConfiguration>> GetAsync(ClientId id, Ct ct);

    /// <summary>
    /// Gets a client by its OAuth <c>client_id</c> string.
    /// </summary>
    /// <param name="clientId">The OAuth client_id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<ClientConfiguration>> GetByClientIdAsync(string clientId, Ct ct);

    /// <summary>
    /// Updates an existing client. Secret metadata cannot be updated.
    /// To add or change secret values, use <see cref="CreateSecretAsync"/> and <see cref="DeleteSecretAsync"/>.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="client">The updated client definition.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<ClientId>> UpdateAsync(ClientId id, UpdateClient client, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes a client.
    /// </summary>
    /// <param name="id">The storage identifier of the client to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<ClientId>> DeleteAsync(ClientId id, Ct ct);

    /// <summary>
    /// Queries clients with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<ClientListItem>> QueryAsync(QueryRequest<ClientFilter, ClientSortField> request, Ct ct);

    // === Secret Management ===

    /// <summary>
    /// Creates a new secret for a client. The plaintext value is hashed before storage.
    /// Secret metadata cannot be updated. To change a secret, delete it and create a new one.
    /// </summary>
    /// <param name="clientId">The storage ID of the client.</param>
    /// <param name="secret">The secret to create. The plaintext value is hashed before storage.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new secret's storage <see cref="SecretId"/> on success, or errors on failure.</returns>
    Task<SaveResult<SecretId>> CreateSecretAsync(ClientId clientId, CreateClientSecret secret, Ct ct);

    /// <summary>
    /// Deletes a secret from a client.
    /// </summary>
    /// <param name="clientId">The storage ID of the client.</param>
    /// <param name="secretId">The storage ID of the secret to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<SecretId>> DeleteSecretAsync(ClientId clientId, SecretId secretId, Ct ct);
}
