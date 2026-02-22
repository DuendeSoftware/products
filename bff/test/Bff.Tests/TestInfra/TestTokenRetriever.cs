// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Tests.TestInfra;

public class TestTokenRetriever : IAccessTokenRetriever
{

    public AccessTokenRetrievalContext? UsedContext { get; set; }

    public Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context,
        Ct ct = default)
    {
        UsedContext = context;
        return Task.FromResult<AccessTokenResult>(new NoAccessTokenResult());
    }
}
