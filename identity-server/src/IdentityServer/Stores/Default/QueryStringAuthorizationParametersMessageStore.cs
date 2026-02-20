// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

// internal just for testing
internal class QueryStringAuthorizationParametersMessageStore : IAuthorizationParametersMessageStore
{
    public Task<string> WriteAsync(Message<IDictionary<string, string[]>> message, CT ct)
    {
        var queryString = message.Data.FromFullDictionary().ToQueryString();
        return Task.FromResult(queryString);
    }

    public Task<Message<IDictionary<string, string[]>>> ReadAsync(string id, CT ct)
    {
        var values = id.ReadQueryStringAsNameValueCollection();
        var msg = new Message<IDictionary<string, string[]>>(values.ToFullDictionary());
        return Task.FromResult(msg);
    }

    public Task DeleteAsync(string id, CT ct) => Task.CompletedTask;
}
