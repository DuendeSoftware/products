// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

internal class ConsentMessageStore : IConsentMessageStore
{
    protected readonly MessageCookie<ConsentResponse> Cookie;

    public ConsentMessageStore(MessageCookie<ConsentResponse> cookie) => Cookie = cookie;

    public virtual Task DeleteAsync(string id, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ConsentMessageStore.Delete");

        Cookie.Clear(id);
        return Task.CompletedTask;
    }

    public virtual Task<Message<ConsentResponse>> ReadAsync(string id, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ConsentMessageStore.Read");

        return Task.FromResult(Cookie.Read(id));
    }

    public virtual Task WriteAsync(string id, Message<ConsentResponse> message, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ConsentMessageStore.Write");

        Cookie.Write(id, message);
        return Task.CompletedTask;
    }
}
