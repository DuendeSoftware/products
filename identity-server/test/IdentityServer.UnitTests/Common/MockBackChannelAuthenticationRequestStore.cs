// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Common;

public class MockBackChannelAuthenticationRequestStore : IBackChannelAuthenticationRequestStore
{
    public Dictionary<string, BackChannelAuthenticationRequest> Items { get; set; } = new Dictionary<string, BackChannelAuthenticationRequest>();

    public Task<string> CreateRequestAsync(BackChannelAuthenticationRequest request, CT _)
    {
        var key = Guid.NewGuid().ToString();
        request.InternalId = key.Sha256();
        Items.Add(key, request);
        return Task.FromResult(key);
    }

    public Task<BackChannelAuthenticationRequest> GetByAuthenticationRequestIdAsync(string requestId, CT _) => Task.FromResult(Items[requestId]);

    public Task<BackChannelAuthenticationRequest> GetByInternalIdAsync(string id, CT _)
    {
        var item = Items.SingleOrDefault(x => x.Value.InternalId == id);
        return Task.FromResult(item.Value);
    }

    public Task<IEnumerable<BackChannelAuthenticationRequest>> GetLoginsForUserAsync(string subjectId, CT ct, string clientId = null)
    {
        var items = Items.Where(x => x.Value.Subject.GetSubjectId() == subjectId
                                     && (clientId == null || x.Value.ClientId == clientId)
        );
        return Task.FromResult(items.Select(x => x.Value).AsEnumerable());
    }

    public Task RemoveByInternalIdAsync(string id, CT _)
    {
        var item = Items.SingleOrDefault(x => x.Value.InternalId == id);
        if (item.Key != null)
        {
            Items.Remove(item.Key);
        }
        return Task.CompletedTask;
    }

    public Task UpdateByInternalIdAsync(string id, BackChannelAuthenticationRequest request, CT _)
    {
        var item = Items.SingleOrDefault(x => x.Value.InternalId == id);
        if (item.Key != null)
        {
            Items.Remove(item.Key);
            Items.Add(item.Key, request);
        }
        return Task.CompletedTask;
    }
}
