// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Common;

internal class MockReferenceTokenStore : IReferenceTokenStore
{
    public Task<Token> GetReferenceTokenAsync(string handle)
    {
        throw new NotImplementedException();
    }

    public Task RemoveReferenceTokenAsync(string handle)
    {
        throw new NotImplementedException();
    }

    public Task RemoveReferenceTokensAsync(string subjectId, string clientId, string sessionId = null)
    {
        throw new NotImplementedException();
    }

    public Task<string> StoreReferenceTokenAsync(Token token)
    {
        throw new NotImplementedException();
    }
}
