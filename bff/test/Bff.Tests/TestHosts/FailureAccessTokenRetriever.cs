// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Tests.TestHosts;

public class FailureAccessTokenRetriever : IAccessTokenRetriever
{
    public Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context, CT ct = default) =>
        Task.FromResult<AccessTokenResult>(new AccessTokenRetrievalError
        {
            Error = "no access token"
        });
}
