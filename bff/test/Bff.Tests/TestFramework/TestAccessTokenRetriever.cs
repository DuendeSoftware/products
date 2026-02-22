// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Tests.TestFramework;

public class TestAccessTokenRetriever(Func<Task<AccessTokenResult>> accessTokenGetter) : IAccessTokenRetriever
{
    public async Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context, Ct ct = default) => await accessTokenGetter();
}
