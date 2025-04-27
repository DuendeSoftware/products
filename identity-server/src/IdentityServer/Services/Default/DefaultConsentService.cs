// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default consent service
/// </summary>
public class DefaultConsentService : IConsentService
{
    /// <summary>
    /// The user consent store
    /// </summary>
    protected readonly IUserConsentStore UserConsentStore;

    /// <summary>
    ///  The clock
    /// </summary>
    protected readonly IClock Clock;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger<DefaultConsentService> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConsentService" /> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="userConsentStore">The user consent store.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="System.ArgumentNullException">store</exception>
    public DefaultConsentService(IClock clock, IUserConsentStore userConsentStore, ILogger<DefaultConsentService> logger)
    {
        Clock = clock;
        UserConsentStore = userConsentStore;
        Logger = logger;
    }

    /// <summary>
    /// Checks if consent is required.
    /// </summary>
    /// <param name="subject">The user.</param>
    /// <param name="client">The client.</param>
    /// <param name="parsedScopes">The parsed scopes.</param>
    /// <returns>
    /// Boolean if consent is required.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// client
    /// or
    /// subject
    /// </exception>
    public virtual async Task<bool> RequiresConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<ParsedScopeValue> parsedScopes)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultConsentService.RequiresConsent");

        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(subject);

        if (!client.RequireConsent)
        {
            Logger.LogDebug("Client is configured to not require consent, no consent is required");
            return false;
        }

        if (parsedScopes == null || !parsedScopes.Any())
        {
            Logger.LogDebug("No scopes being requested, no consent is required");
            return false;
        }

        if (!client.AllowRememberConsent)
        {
            Logger.LogDebug("Client is configured to not allow remembering consent, consent is required");
            return true;
        }

        if (parsedScopes.Any(x => x.ParsedName != x.RawValue))
        {
            Logger.LogDebug("Scopes contains parameterized values, consent is required");
            return true;
        }

        var scopes = parsedScopes.Select(x => x.RawValue).ToArray();

        // we always require consent for offline access if
        // the client has not disabled RequireConsent 
        if (scopes.Contains(IdentityServerConstants.StandardScopes.OfflineAccess))
        {
            Logger.LogDebug("Scopes contains offline_access, consent is required");
            return true;
        }

        var consent = await UserConsentStore.GetUserConsentAsync(subject.GetSubjectId(), client.ClientId);

        if (consent == null)
        {
            Logger.LogDebug("Found no prior consent from consent store, consent is required");
            return true;
        }

        if (consent.Expiration.HasExpired(Clock.UtcNow.UtcDateTime))
        {
            Logger.LogDebug("Consent found in consent store is expired, consent is required");
            await UserConsentStore.RemoveUserConsentAsync(consent.SubjectId, consent.ClientId);
            return true;
        }

        if (consent.Scopes != null)
        {
            var intersect = scopes.Intersect(consent.Scopes);
            var different = scopes.Length != intersect.Count();

            if (different)
            {
                Logger.LogDebug("Consent found in consent store is different than current request, consent is required");
            }
            else
            {
                Logger.LogDebug("Consent found in consent store is same as current request, consent is not required");
            }

            return different;
        }

        Logger.LogDebug("Consent found in consent store has no scopes, consent is required");

        return true;
    }

    /// <summary>
    /// Updates the consent asynchronous.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="parsedScopes">The parsed scopes.</param>
    /// <exception cref="System.ArgumentNullException">
    /// client
    /// or
    /// subject
    /// </exception>
    public virtual async Task UpdateConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<ParsedScopeValue> parsedScopes)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultConsentService.UpdateConsent");

        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(subject);

        if (client.AllowRememberConsent)
        {
            var subjectId = subject.GetSubjectId();
            var clientId = client.ClientId;

            var scopes = parsedScopes?.Select(x => x.RawValue).ToArray();
            if (scopes != null && scopes.Any())
            {
                Logger.LogDebug("Client allows remembering consent, and consent given. Updating consent store for subject: {subject}", subject.GetSubjectId());

                var consent = new Consent
                {
                    CreationTime = Clock.UtcNow.UtcDateTime,
                    SubjectId = subjectId,
                    ClientId = clientId,
                    Scopes = scopes
                };

                if (client.ConsentLifetime.HasValue)
                {
                    consent.Expiration = consent.CreationTime.AddSeconds(client.ConsentLifetime.Value);
                }

                await UserConsentStore.StoreUserConsentAsync(consent);
            }
            else
            {
                Logger.LogDebug("Client allows remembering consent, and no scopes provided. Removing consent from consent store for subject: {subject}", subject.GetSubjectId());

                await UserConsentStore.RemoveUserConsentAsync(subjectId, clientId);
            }
        }
    }
}
