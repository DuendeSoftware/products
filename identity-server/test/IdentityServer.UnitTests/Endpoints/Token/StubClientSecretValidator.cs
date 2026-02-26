// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;

namespace IdentityServer.Endpoints.Token;

internal class StubClientSecretValidator : IClientSecretValidator
{
    public ClientSecretValidationResult Result { get; set; }

    public Task<ClientSecretValidationResult> ValidateAsync(HttpContext context, Ct _) => Task.FromResult(Result);
}
