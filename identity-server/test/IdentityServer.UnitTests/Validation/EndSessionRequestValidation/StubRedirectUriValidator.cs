// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.EndSessionRequestValidation;

public class StubRedirectUriValidator : IRedirectUriValidator
{
    public bool IsRedirectUriValid { get; set; }
    public bool IsPostLogoutRedirectUriValid { get; set; }

    public Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client, CT ct) => Task.FromResult(IsPostLogoutRedirectUriValid);

#pragma warning disable CS0618
    public Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client) => Task.FromResult(IsRedirectUriValid);
#pragma warning restore CS0618
}
