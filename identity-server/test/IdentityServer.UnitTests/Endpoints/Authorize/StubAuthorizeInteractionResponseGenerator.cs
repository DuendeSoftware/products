// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Validation;

namespace UnitTests.Endpoints.Authorize;

internal class StubAuthorizeInteractionResponseGenerator : IAuthorizeInteractionResponseGenerator
{
    internal InteractionResponse Response { get; set; } = new InteractionResponse();

    public Task<InteractionResponse> ProcessInteractionAsync(ValidatedAuthorizeRequest request, ConsentResponse consent = null) => Task.FromResult(Response);
}
