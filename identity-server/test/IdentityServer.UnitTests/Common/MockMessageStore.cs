// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Common;

public class MockMessageStore<TModel> : IMessageStore<TModel>
{
    public Dictionary<string, Message<TModel>> Messages { get; set; } = new Dictionary<string, Message<TModel>>();

    public Task<Message<TModel>> ReadAsync(string id, CT ct)
    {
        Message<TModel> val = null;
        if (id != null)
        {
            Messages.TryGetValue(id, out val);
        }
        return Task.FromResult(val);
    }

    public Task<string> WriteAsync(Message<TModel> message, CT ct)
    {
        var id = Guid.NewGuid().ToString();
        Messages[id] = message;
        return Task.FromResult(id);
    }
}
