// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Tests.TestHosts;

public class TestAccessTokenRetriever : IAccessTokenRetriever
{
    public TestAccessTokenRetriever(Func<Task<AccessTokenResult>> accessTokenGetter) => _accessTokenGetter = accessTokenGetter;

    private readonly Func<Task<AccessTokenResult>> _accessTokenGetter;

    public async Task<AccessTokenResult> GetAccessToken(AccessTokenRetrievalContext context) => await _accessTokenGetter();
}
