// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Default reference token store.
/// </summary>
public class DefaultReferenceTokenStore : DefaultGrantStore<Token>, IReferenceTokenStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultReferenceTokenStore"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="handleGenerationService">The handle generation service.</param>
    /// <param name="logger">The logger.</param>
    public DefaultReferenceTokenStore(
        IPersistedGrantStore store,
        IPersistentGrantSerializer serializer,
        IHandleGenerationService handleGenerationService,
        ILogger<DefaultReferenceTokenStore> logger)
        : base(IdentityServerConstants.PersistedGrantTypes.ReferenceToken, store, serializer, handleGenerationService, logger)
    {
    }

    /// <inheritdoc/>
    public Task<string> StoreReferenceTokenAsync(Token token, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultReferenceTokenStore.StoreReferenceToken");

        return CreateItemAsync(token, token.ClientId, token.SubjectId, token.SessionId, token.Description, token.CreationTime, token.Lifetime, ct);
    }

    /// <inheritdoc/>
    public Task<Token> GetReferenceTokenAsync(string handle, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultReferenceTokenStore.GetReferenceToken");

        return GetItemAsync(handle, ct);
    }

    /// <inheritdoc/>
    public Task RemoveReferenceTokenAsync(string handle, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultReferenceTokenStore.RemoveReferenceToken");

        return RemoveItemAsync(handle, ct);
    }

    /// <inheritdoc/>
    public Task RemoveReferenceTokensAsync(string subjectId, string clientId, string sessionId, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultReferenceTokenStore.RemoveReferenceTokens");

        return RemoveAllAsync(subjectId, clientId, sessionId, ct);
    }
}
