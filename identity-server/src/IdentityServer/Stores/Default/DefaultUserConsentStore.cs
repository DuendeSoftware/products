// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Default user consent store.
/// </summary>
public class DefaultUserConsentStore : DefaultGrantStore<Consent>, IUserConsentStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserConsentStore"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="handleGenerationService">The handle generation service.</param>
    /// <param name="logger">The logger.</param>
    public DefaultUserConsentStore(
        IPersistedGrantStore store,
        IPersistentGrantSerializer serializer,
        IHandleGenerationService handleGenerationService,
        ILogger<DefaultUserConsentStore> logger)
        : base(IdentityServerConstants.PersistedGrantTypes.UserConsent, store, serializer, handleGenerationService, logger)
    {
    }

    private static string GetConsentKey(string subjectId, string clientId, bool useHexEncoding = true)
    {
        if (useHexEncoding)
        {
            return $"{clientId}|{subjectId}{HexEncodingFormatSuffix}";
        }
        else
        {
            return $"{clientId}|{subjectId}";
        }
    }

    /// <summary>
    /// Stores the user consent asynchronously.
    /// </summary>
    /// <param name="consent">The consent.</param>
    /// <returns></returns>
    public Task StoreUserConsentAsync(Consent consent)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultUserConsentStore.StoreUserConsent");

        var key = GetConsentKey(consent.SubjectId, consent.ClientId);
        return StoreItemAsync(key, consent, consent.ClientId, consent.SubjectId, null, null, consent.CreationTime, consent.Expiration);
    }

    /// <summary>
    /// Gets the user consent asynchronously.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <returns></returns>
    public async Task<Consent> GetUserConsentAsync(string subjectId, string clientId)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultUserConsentStore.GetUserConsent");

        var key = GetConsentKey(subjectId, clientId);
        var consent = await GetItemAsync(key);
        if (consent == null)
        {
            var legacyKey = GetConsentKey(subjectId, clientId, useHexEncoding: false);
            consent = await GetItemAsync(legacyKey);
            if (consent != null)
            {
                await StoreUserConsentAsync(consent); // Write back the consent record to update its key
                await RemoveItemAsync(legacyKey);
            }
        }

        return consent;
    }

    /// <summary>
    /// Removes the user consent asynchronously.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <returns></returns>
    public Task RemoveUserConsentAsync(string subjectId, string clientId)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultUserConsentStore.RemoveUserConsent");

        var key = GetConsentKey(subjectId, clientId);
        return RemoveItemAsync(key);
    }
}
