// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.EndSessionRequestValidation;

public class StubRedirectUriValidator : IRedirectUriValidator
{
    public bool IsRedirectUriValid { get; set; }
    public bool IsPostLogoutRedirectUriValid { get; set; }

    public Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client) => Task.FromResult(IsPostLogoutRedirectUriValid);

    public Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client) => Task.FromResult(IsRedirectUriValid);
}
