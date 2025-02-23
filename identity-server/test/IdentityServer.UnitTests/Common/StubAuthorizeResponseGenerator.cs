// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Threading.Tasks;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Validation;

namespace UnitTests.Common;

internal class StubAuthorizeResponseGenerator : IAuthorizeResponseGenerator
{
    public AuthorizeResponse Response { get; set; } = new AuthorizeResponse();

    public Task<AuthorizeResponse> CreateResponseAsync(ValidatedAuthorizeRequest request)
    {
        return Task.FromResult(Response);
    }
}