// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default persisted grant service
/// </summary>
public class DefaultPersistedGrantService : IPersistedGrantService
{
    private readonly ILogger _logger;
    private readonly IPersistedGrantStore _store;
    private readonly IPersistentGrantSerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPersistedGrantService"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="logger">The logger.</param>
    public DefaultPersistedGrantService(IPersistedGrantStore store,
        IPersistentGrantSerializer serializer,
        ILogger<DefaultPersistedGrantService> logger)
    {
        _store = store;
        _serializer = serializer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Grant>> GetAllGrantsAsync(string subjectId, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultPersistedGrantService.GetAllGrants");

        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var grants = (await _store.GetAllAsync(new PersistedGrantFilter { SubjectId = subjectId }, ct))
            .Where(x => x.ConsumedTime == null) // filter consumed grants
            .ToArray();

        var errors = new List<Exception>();

        T DeserializeAndCaptureErrors<T>(string data)
        {
            try
            {
                return _serializer.Deserialize<T>(data);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                return default(T);
            }
        }

        try
        {
            var consents = grants.Where(x => x.Type == IdentityServerConstants.PersistedGrantTypes.UserConsent)
                .Select(x => DeserializeAndCaptureErrors<Consent>(x.Data))
                .Where(x => x != default)
                .Select(x => new Grant
                {
                    ClientId = x.ClientId,
                    SubjectId = subjectId,
                    Scopes = x.Scopes,
                    CreationTime = x.CreationTime,
                    Expiration = x.Expiration
                });

            var codes = grants.Where(x => x.Type == IdentityServerConstants.PersistedGrantTypes.AuthorizationCode)
                .Select(x => DeserializeAndCaptureErrors<AuthorizationCode>(x.Data))
                .Where(x => x != default)
                .Select(x => new Grant
                {
                    ClientId = x.ClientId,
                    SubjectId = subjectId,
                    Description = x.Description,
                    Scopes = x.RequestedScopes,
                    CreationTime = x.CreationTime,
                    Expiration = x.CreationTime.AddSeconds(x.Lifetime)
                });

            var refresh = grants.Where(x => x.Type == IdentityServerConstants.PersistedGrantTypes.RefreshToken)
                .Select(x => DeserializeAndCaptureErrors<RefreshToken>(x.Data))
                .Where(x => x != default)
                .Select(x => new Grant
                {
                    ClientId = x.ClientId,
                    SubjectId = subjectId,
                    Description = x.Description,
                    Scopes = x.AuthorizedScopes,
                    CreationTime = x.CreationTime,
                    Expiration = x.CreationTime.AddSeconds(x.Lifetime)
                });

            var access = grants.Where(x => x.Type == IdentityServerConstants.PersistedGrantTypes.ReferenceToken)
                .Select(x => DeserializeAndCaptureErrors<Token>(x.Data))
                .Where(x => x != default)
                .Select(x => new Grant
                {
                    ClientId = x.ClientId,
                    SubjectId = subjectId,
                    Description = x.Description,
                    Scopes = x.Scopes,
                    CreationTime = x.CreationTime,
                    Expiration = x.CreationTime.AddSeconds(x.Lifetime)
                });

            consents = Join(consents, codes);
            consents = Join(consents, refresh);
            consents = Join(consents, access);

            if (errors.Count > 0)
            {
                _logger.LogError(new AggregateException(errors), "One or more errors occured during deserialization of persisted grants, returning successfull items.");
            }

            return consents.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing results from grant store.");
        }

        return Enumerable.Empty<Grant>();
    }

    private static List<Grant> Join(IEnumerable<Grant> first, IEnumerable<Grant> second)
    {
        var list = first.ToList();

        foreach (var other in second)
        {
            var match = list.FirstOrDefault(x => x.ClientId == other.ClientId);
            if (match != null)
            {
                match.Scopes = match.Scopes.Union(other.Scopes);

                if (match.CreationTime > other.CreationTime)
                {
                    // show the earlier creation time
                    match.CreationTime = other.CreationTime;
                }

                if (match.Expiration == null || other.Expiration == null)
                {
                    // show that there is no expiration to one of the grants
                    match.Expiration = null;
                }
                else if (match.Expiration < other.Expiration)
                {
                    // show the latest expiration
                    match.Expiration = other.Expiration;
                }

                match.Description = match.Description ?? other.Description;
            }
            else
            {
                list.Add(other);
            }
        }

        return list;
    }

    /// <inheritdoc/>
    public Task RemoveAllGrantsAsync(string subjectId, Ct ct, string clientId = null, string sessionId = null)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultPersistedGrantService.RemoveAllGrants");

        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        return _store.RemoveAllAsync(new PersistedGrantFilter
        {
            SubjectId = subjectId,
            ClientId = clientId,
            SessionId = sessionId
        }, ct);
    }
}
