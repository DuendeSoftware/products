// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Default authorization code store.
/// </summary>
public class DefaultBackChannelAuthenticationRequestStore : DefaultGrantStore<BackChannelAuthenticationRequest>, IBackChannelAuthenticationRequestStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationCodeStore"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="handleGenerationService">The handle generation service.</param>
    /// <param name="logger">The logger.</param>
    public DefaultBackChannelAuthenticationRequestStore(
        IPersistedGrantStore store,
        IPersistentGrantSerializer serializer,
        IHandleGenerationService handleGenerationService,
        ILogger<DefaultBackChannelAuthenticationRequestStore> logger)
        : base(IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest, store, serializer, handleGenerationService, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<string> CreateRequestAsync(BackChannelAuthenticationRequest request, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultBackChannelAuthenticationRequestStore.CreateRequest");

        var handle = await CreateHandleAsync(ct);
        request.InternalId = GetHashedKey(handle);
        await StoreItemByHashedKeyAsync(request.InternalId, request, request.ClientId, request.Subject.GetSubjectId(), null, null, request.CreationTime, request.CreationTime.AddSeconds(request.Lifetime), ct: ct);
        return handle;
    }

    /// <inheritdoc/>
    public Task<BackChannelAuthenticationRequest> GetByInternalIdAsync(string id, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultBackChannelAuthenticationRequestStore.GetByInternalId");

        return GetItemByHashedKeyAsync(id, ct);
    }

    /// <inheritdoc/>
    public Task<BackChannelAuthenticationRequest> GetByAuthenticationRequestIdAsync(string requestId, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultBackChannelAuthenticationRequestStore.GetByAuthenticationRequestId");

        return GetItemAsync(requestId, ct);
    }

    /// <inheritdoc/>
    public Task RemoveByInternalIdAsync(string requestId, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultBackChannelAuthenticationRequestStore.RemoveByInternalId");

        return RemoveItemByHashedKeyAsync(requestId, ct);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<BackChannelAuthenticationRequest>> GetLoginsForUserAsync(string subjectId, string clientId = null, CT ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultBackChannelAuthenticationRequestStore.GetLoginsForUser");

        return GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = subjectId,
            ClientId = clientId,
        }, ct);
    }

    /// <inheritdoc/>
    public Task UpdateByInternalIdAsync(string id, BackChannelAuthenticationRequest request, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultBackChannelAuthenticationRequestStore.UpdateByInternalId");

        return StoreItemByHashedKeyAsync(id, request, request.ClientId, request.Subject.GetSubjectId(), request.SessionId, request.Description, request.CreationTime, request.CreationTime.AddSeconds(request.Lifetime), ct: ct);
    }
}
